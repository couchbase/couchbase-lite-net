//
// ForestDBCouchStore.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#define FAKE_ENCRYPTION
#if FORESTDB

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using CBForest;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Store
{

    #region Delegates

    internal unsafe delegate void C4DocumentActionDelegate(C4Document* doc);

    internal unsafe delegate void C4RawDocumentActionDelegate(C4RawDocument *doc);

    internal unsafe delegate bool C4TryLogicDelegate1(C4Error* err);

    internal unsafe delegate void* C4TryLogicDelegate2(C4Error *err);

    internal unsafe delegate bool C4RevisionSelector(C4Document *doc);

    #endregion

    #region CBForestException

    /// <summary>
    /// An exception representing an error status from the native
    /// CBForest module
    /// </summary>
    public sealed class CBForestException : ApplicationException
    {

        #region Variables

        private readonly C4ErrorCode _code;
        private readonly C4ErrorDomain _domain;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the error code received from CBForest
        /// </summary>
        public C4ErrorCode Code 
        {
            get {
                return _code;
            }
        }

        /// <summary>
        /// Gets the domain of the error code received from CBForest
        /// </summary>
        /// <value>The domain.</value>
        public C4ErrorDomain Domain
        {
            get {
                return _domain;
            }
        }

        #endregion

        #region Constructors

        internal CBForestException(C4ErrorCode code, C4ErrorDomain domain) 
            : base(String.Format("CBForest exception ({0} : {1})", domain, code))
        {
            _code = code;
            _domain = domain;
        }

        #endregion

    }

    #endregion

    #region ForestDBBridge

    internal unsafe static class ForestDBBridge {
        public static void Check(C4TryLogicDelegate1 block)
        {
            var err = default(C4Error);
            if (block(&err)) {
                return;
            }

            throw new CouchbaseLiteException(new CBForestException((C4ErrorCode)err.code, err.domain), StatusCode.DbError);
        }

        public static void* Check(C4TryLogicDelegate2 block)
        {
            var err = default(C4Error);
            var obj = block(&err);
            if (obj != null) {
                return obj;
            }

            throw new CouchbaseLiteException(new CBForestException((C4ErrorCode)err.code, err.domain), StatusCode.DbError);
        }

    }

    #endregion

    #region ForestDBCouchStore

    internal unsafe sealed class ForestDBCouchStore : ICouchStore
    {

        #region Constants

        private const int DEFAULT_MAX_REV_TREE_DEPTH = 20;
        private const string DB_FILENAME = "db.forest";
        private const string TAG = "ForestDBCouchStore";

        #endregion

        #region Variables

        private string _directory;
        private C4DatabaseFlags _config;
        private C4Database *_forest;
        private SymmetricKey _encryptionKey;
        private int _transactionLevel;

        #endregion

        #region Properties

        public bool AutoCompact { get; set; }

        public int MaxRevTreeDepth { get; set; }

        public ICouchStoreDelegate Delegate { get; set; }

        public int DocumentCount
        {
            get {
                return (int)Native.c4db_getDocumentCount(_forest);
            }
        }

        public long LastSequence
        {
            get {
                return (long)Native.c4db_getLastSequence(_forest);
            }
        }

        public bool InTransaction
        {
            get {
                return Native.c4db_isInTransaction(_forest);
            }
        }

        #endregion

        #region Constructors

        public ForestDBCouchStore()
        {
            AutoCompact = true;
            MaxRevTreeDepth = DEFAULT_MAX_REV_TREE_DEPTH;
        }

        #endregion

        #region Public Methods

        public void GetDocument(string docId, long sequence)
        {
            throw new NotSupportedException("C API lacks this feature");
        } 

        #endregion

        #region Private Methods

        private void WithC4Document(string docId, string revId, bool withBody, C4DocumentActionDelegate block)
        {
            var doc = (C4Document*)ForestDBBridge.Check(err => Native.c4doc_get(_forest, docId, true, err));
            ForestDBBridge.Check(err => Native.c4doc_selectRevision(doc, revId, withBody, err));

            try {
                block(doc);
            } finally {
                Native.c4doc_free(doc);
            }
        }

        private void WithC4Raw(string docId, string storeName, C4RawDocumentActionDelegate block)
        {
            var doc = (C4RawDocument*)ForestDBBridge.Check(err =>
                Native.c4raw_get(_forest, storeName, docId, err));

            try {
                block(doc);
            } finally {
                Native.c4raw_free(doc);
            }
        }

        private static IEnumerable<RevisionInternal> EnumerateHistory(IntPtr docPtr, bool includeDeleted, bool onlyCurrent, bool withBodies)
        {
            if(docPtr == IntPtr.Zero) {
                yield break;
            }

            var doc = (C4Document*)docPtr.ToPointer();
            yield return new RevisionInternal(doc, withBodies);

            if (onlyCurrent) {
                while (Native.c4doc_selectNextLeafRevision(doc, includeDeleted, withBodies, null)) {
                    yield return new RevisionInternal(doc, withBodies);
                }
            } else {
                while (Native.c4doc_selectNextRevision(doc)) {
                    yield return new RevisionInternal(doc, withBodies);
                }
            }
        }

        private void Reopen()
        {
            var nativeKey = default(C4EncryptionKey);
            if (_encryptionKey != null) {
#if FAKE_ENCRYPTION
                nativeKey.algorithm = (C4EncryptionType)(-1);
#else
                nativeKey.algorithm = C4EncryptionType.AES256;
#endif
                nativeKey.bytes = _encryptionKey.KeyData;
            }

            var forestPath = Path.Combine(_directory, DB_FILENAME);
            var nativeKeyPtr = &nativeKey;
            _forest = (C4Database*)ForestDBBridge.Check(err => Native.c4db_open(forestPath, _config, nativeKeyPtr, err));
        }

        private void DeleteLocalRevision(string docId, string revId, bool obeyMVCC)
        {
            if (!docId.StartsWith("_local/")) {
                throw new CouchbaseLiteException("Local revision IDs must start with _local/", StatusCode.BadId);
            }

            if (obeyMVCC && revId == null) {
                // Didn't specify a revision to delete: NotFound or a Conflict, depending
                var gotLocalDoc = GetLocalDocument(docId, null);
                if(gotLocalDoc == null) {
                    throw new CouchbaseLiteException(StatusCode.NotFound);
                }

                throw new CouchbaseLiteException(StatusCode.Conflict);
            }

            RunInTransaction(() =>
            {
                var doc = (C4RawDocument *)ForestDBBridge.Check(err => Native.c4raw_get(_forest, "_local", docId, err));
                if(doc == null) {
                    throw new CouchbaseLiteException(StatusCode.NotFound);
                }

                if(obeyMVCC && (revId != (string)doc->meta)) {
                    throw new CouchbaseLiteException(StatusCode.Conflict);
                }

                ForestDBBridge.Check(err => Native.c4raw_put(_forest, "_local", docId, null, null, err));
                return true;
            });
        }

        private bool SaveDocument(C4Document *doc, string revId, IDictionary<string, object> properties)
        {
            // Is the new revision the winner?
            Native.c4doc_selectCurrentRevision(doc);
            bool isWinner = (string)doc->selectedRev.revID == revId;

            // Update the documentType:
            if (isWinner) {
                ForestDBBridge.Check(err => Native.c4doc_setType(doc, properties.GetCast<string>("type"), err));
            }

            // Save:
            ForestDBBridge.Check(err => Native.c4doc_save(doc, (uint)MaxRevTreeDepth, err));
            return isWinner;
        }

        private DocumentChange ChangeWithNewRevision(RevisionInternal inRev, bool isWinningRev, C4Document *doc, Uri source)
        {
            var winningRevId = default(string);
            if(isWinningRev) {
                winningRevId = inRev.GetRevId();
            } else {
                winningRevId = (string)doc->revID;
            }

            return new DocumentChange(inRev, winningRevId, doc->IsConflicted, source);
        }

        #endregion

        #region ICouchStore

        public bool DatabaseExistsIn(string directory)
        {
            var dbPath = Path.Combine(directory, DB_FILENAME);
            return File.Exists(dbPath);
        }

        public void Open(string directory, Manager manager, bool readOnly)
        {
            _directory = directory;
            _config = readOnly ? C4DatabaseFlags.ReadOnly : C4DatabaseFlags.Create;
            if (AutoCompact) {
                _config &= C4DatabaseFlags.AutoCompact;
            }

            Reopen();
        }

        public void Close()
        {
            ForestDBBridge.Check(err => Native.c4db_close(_forest, err));
            _forest = null;
        }

        public void SetEncryptionKey(SymmetricKey key)
        {
            _encryptionKey = key;
        }

        public AtomicAction ActionToChangeEncryptionKey(SymmetricKey newKey)
        {
            var action = new AtomicAction();

            // Copy the database to a temporary file using the new encryption:
            var tempPath = Path.Combine(Path.GetTempPath(), Misc.CreateGUID());
            action.AddLogic(() =>
            {

            }, () =>
            {

            }, null);

            throw new NotImplementedException("C API needs a copy function");
        }

        public void Compact()
        {
            throw new NotImplementedException("C API needs a compact function");
        }

        public void RunInTransaction(RunInTransactionDelegate block)
        {
            Log.D(TAG, "BEGIN transaction...");
            _transactionLevel++;
            ForestDBBridge.Check(err => Native.c4db_beginTransaction(_forest, err));
            var success = false;
            try {
                success = block();
            } catch(Exception e) {
                Log.E(TAG, "Exception in RunInTransaction block", e);
                success = false;
            }

            Log.D(TAG, "END transaction (success={0})", success);
            ForestDBBridge.Check(err => Native.c4db_endTransaction(_forest, success, err));
            if (--_transactionLevel == 0 && Delegate != null) {
                Delegate.StorageExitedTransaction(success);
            }
        }

        public RevisionInternal GetDocument(string docId, string revId, bool withBody)
        {
            var retVal = default(RevisionInternal);
            WithC4Document(docId, revId, withBody, doc =>
            {
                retVal = new RevisionInternal(doc, withBody);
            });

            return retVal;
        }

        public void LoadRevisionBody(RevisionInternal rev)
        {
            WithC4Document(rev.GetDocId(), rev.GetRevId(), true, doc => rev.SetBody(new Body(doc->selectedRev.body)));
        }

        public long GetRevisionSequence(RevisionInternal rev)
        {
            var retVal = 0L;
            WithC4Document(rev.GetDocId(), rev.GetRevId(), false, doc => retVal = (long)doc->selectedRev.sequence);

            return retVal;
        }

        public RevisionInternal GetParentRevision(RevisionInternal rev)
        {
            var retVal = rev;
            WithC4Document(rev.GetDocId(), rev.GetRevId(), false, doc =>
            {
                if (!Native.c4doc_selectParentRevision(doc)) {
                    return;
                }
                    
                ForestDBBridge.Check(err => Native.c4doc_loadRevisionBody(doc, err));
                retVal = rev.CopyWithDocID(rev.GetDocId(), (string)doc->selectedRev.revID);
                retVal.SetSequence((long)doc->selectedRev.sequence);
                retVal.SetBody(new Body(doc->selectedRev.body));
            });

            return retVal;
        }

        public RevisionList GetAllDocumentRevisions(string docId, bool onlyCurrent)
        {
            var retVal = default(RevisionList);
            WithC4Document(docId, null, false, doc =>
            {
                retVal = new RevisionList(EnumerateHistory(new IntPtr(doc), true, onlyCurrent, false).ToList());
            });

            return retVal;
        }

        public IEnumerable<string> GetPossibleAncestors(RevisionInternal rev, int limit, bool onlyAttachments)
        {
            var generation = RevisionInternal.GenerationFromRevID(rev.GetRevId());
            if (generation <= 1) {
                return null;
            }

            var returnedCount = 0;
            WithC4Document(rev.GetDocId(), null, false, doc =>
            {
                while(Native.c4doc_selectNextRevision(doc)) {
                    
                }
            });

            throw new NotImplementedException("Need additional C API elements");
        }

        public string FindCommonAncestor(RevisionInternal rev, IEnumerable<string> revIds)
        {
            var generation = RevisionInternal.GenerationFromRevID(rev.GetRevId());
            var revIdArray = revIds.ToList();
            if (generation <= 1 || revIdArray.Count == 0) {
                return null;
            }
             
            revIdArray.Sort(RevisionInternal.CBLCompareRevIDs);
            var commonAncestor = default(string);
            WithC4Document(rev.GetDocId(), null, false, doc =>
            {
                foreach(var possibleRevId in revIds) {
                    if(RevisionInternal.GenerationFromRevID(possibleRevId) <= generation &&
                        Native.c4doc_selectRevision(doc, possibleRevId, false, null)) {
                        commonAncestor = possibleRevId;
                        return;
                    }
                }
            });

            return commonAncestor;
        }

        public IList<RevisionInternal> GetRevisionHistory(RevisionInternal rev, ICollection<string> ancestorRevIds)
        {
            var history = default(IList<RevisionInternal>);
            WithC4Document(rev.GetDocId(), null, false, doc =>
            {
                var docId = (string)doc->docID;
                var newRev = new RevisionInternal(doc, false);
                while(!ancestorRevIds.Contains(newRev.GetRevId())) {
                    history.Add(newRev);
                    if(!Native.c4doc_selectParentRevision(doc)) {
                        return;
                    }

                    newRev = new RevisionInternal(doc, false);
                } 
            });

            throw new NotImplementedException("C API lacks isBodyAvailable");
        }

        public RevisionList ChangesSince(Int64 lastSequence, ChangesOptions options, RevisionFilter filter)
        {
            // http://wiki.apache.org/couchdb/HTTP_database_API#Changes
            // Translate options to ForestDB:
            if (options.Descending) {
                // https://github.com/couchbase/couchbase-lite-ios/issues/641
                throw new CouchbaseLiteException(StatusCode.NotImplemented);
            }

            var forestOps = C4ChangesOptions.DEFAULT;
            forestOps.includeDeleted = false;
            forestOps.includeBodies = options.IsIncludeDocs() || options.IsIncludeConflicts() || filter != null;
            var changes = new RevisionList();
            var p = &forestOps;
            var e = (C4DocEnumerator*)ForestDBBridge.Check(err => Native.c4db_enumerateChanges(_forest, (ulong)lastSequence, p, err));
            var doc = (C4Document*)null;
            while((doc = Native.c4enum_nextDocument(e, null)) != null) {
                var revs = default(IEnumerable<RevisionInternal>);
                if (options.IsIncludeConflicts()) {
                    revs = EnumerateHistory(new IntPtr(doc), false, true, forestOps.includeBodies);
                } else {
                    revs = new List<RevisionInternal> { new RevisionInternal(doc, forestOps.includeBodies) };
                }

                foreach (var rev in revs) {
                    Debug.Assert(rev != null);
                    if (filter == null || filter(rev)) {
                        if (!options.IsIncludeDocs()) {
                            rev.SetBody(null);
                        }

                        changes.Add(rev);
                    }
                }

                Native.c4doc_free(doc);
            }

            return changes;
        }

        public IEnumerable<QueryRow> GetAllDocs(QueryOptions options)
        {
            var forestOpts = C4AllDocsOptions.DEFAULT;
            forestOpts.descending = options.Descending;
            forestOpts.inclusiveEnd = options.InclusiveEnd;
            forestOpts.inclusiveStart = options.InclusiveStart;
            var e = (C4DocEnumerator *)ForestDBBridge.Check(err => 
            {
                var f = forestOpts;
                return Native.c4db_enumerateAllDocs(_forest, options.StartKeyDocId, options.EndKeyDocId, &f, err);
            });

            var doc = (C4Document*)null;
            while ((doc = Native.c4enum_nextDocument(e, null)) != null) {
                var docID = (string)doc->docID;
                var conflicts = default(IList<string>);
                if (options.AllDocsMode >= AllDocsMode.ShowConflicts && doc->IsConflicted) {
                    conflicts = EnumerateHistory(new IntPtr(doc), false, true, false).Select(x => x.GetRevId()).ToList();
                    if (conflicts.Count == 1) {
                        conflicts = null;
                    }
                }

                var value = new NonNullDictionary<string, object> {
                    { "rev", (string)doc->selectedRev.revID },
                    { "deleted", doc->IsDeleted ? (object)true : null },
                    { "_conflicts", conflicts }
                };
                yield return new QueryRow(docID, (long)doc->selectedRev.sequence, docID, value, new RevisionInternal(doc, false), null);
            }
        }

        public int FindMissingRevisions(RevisionList revs)
        {
            var sortedRevs = new RevisionList(revs);
            sortedRevs.SortByDocID();
            var lastDocId = (string)null;
            var doc = (C4Document*)null;
            var removedCount = 0;
            try {
                foreach (var rev in sortedRevs) {
                    if (rev.GetDocId() != lastDocId) {
                        lastDocId = rev.GetDocId();
                        Native.c4doc_free(doc);
                        doc = (C4Document *)ForestDBBridge.Check(err => Native.c4doc_get(_forest, lastDocId, true, err));
                    }

                    if (Native.c4doc_selectRevision(doc, rev.GetRevId(), false, null)) {
                        removedCount++;
                        revs.Remove(rev);
                    }
                }
            } finally {
                Native.c4doc_free(doc);
            }

            return removedCount;
        }

        public ICollection<BlobKey> FindAllAttachmentKeys()
        {
            var keys = new HashSet<BlobKey>();
            var options = C4AllDocsOptions.DEFAULT;
            options.includeBodies = false;
            options.includeDeleted = true;
            var p = &options;
            var e = (C4DocEnumerator*)ForestDBBridge.Check(err =>
                Native.c4db_enumerateAllDocs(_forest, null, null, p, err));
            var doc = default(C4Document*);
            while ((doc = Native.c4enum_nextDocument(e, null)) != null) {
                if (!doc->HasAttachments || (doc->IsDeleted && !doc->IsConflicted)) {
                    continue;
                }

                // Since db is assumed to have just been compacted, we know that non-current revisions
                // won't have any bodies. So only scan the current revs.
                do {
                    if(doc->selectedRev.IsActive && doc->selectedRev.HasAttachments) {
                        var body = doc->selectedRev.body;
                        if(body.size.ToUInt32() > 0) {
                            var rev = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(body);
                            foreach(var entry in rev.Get("_attachments").AsDictionary<string, IDictionary<string, object>>()) {
                                try {
                                    var key = new BlobKey(entry.Value.GetCast<string>("digest"));
                                    keys.Add(key);
                                } catch(Exception){}
                            }
                        }
                    }
                } while(Native.c4doc_selectNextLeafRevision(doc, true, true, null));

                Native.c4doc_free(doc);
            }

            return keys;
        }

        public IDictionary<string, object> PurgeRevisions(IDictionary<string, IList<string>> docsToRev)
        {
            // <http://wiki.apache.org/couchdb/Purge_Documents>
            IDictionary<string, object> result = new Dictionary<string, object>();
            if (docsToRev.Count == 0) {
                return result;
            }

            Log.D(TAG, "Purging {0} docs...", docsToRev.Count);
            RunInTransaction(() =>
            {
                foreach(var docRevPair in docsToRev) {
                    var docID = docRevPair.Key;
                    var doc = (C4Document *)ForestDBBridge.Check(err => Native.c4doc_get(_forest, docID, false, err));
                    if(!doc->Exists) {
                        throw new CouchbaseLiteException(StatusCode.NotFound);
                    }

                    var revsPurged = default(IList<string>);
                    var revIDs = docRevPair.Value;
                    if(revIDs.Count == 0) {
                        revsPurged = new List<string>();
                    } else if(revIDs.Contains("*")) {
                        // Delete all revisions if magic "*" revision ID is given:

                    }
                }

                return true;
            });

            throw new NotImplementedException("C API has no way to purge documents");
        }

        public RevisionInternal GetLocalDocument(string docId, string revId)
        {
            if(!docId.StartsWith("_local/")) {
                return null;
            }

            var retVal = default(RevisionInternal);
            WithC4Raw(docId, "_local", doc =>
            {
                if(doc == null) {
                    return;
                }

                var gotRevId = (string)doc->meta;
                if(revId != gotRevId || doc->body.size.ToUInt32() == 0) {
                    return;
                }

                var properties = default(IDictionary<string, object>);
                try {
                    Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(doc->body);
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Invalid JSON for document {0}", docId);
                    return;
                }

                properties["_id"] = docId;
                properties["_rev"] = revId;
                retVal = new RevisionInternal(docId, revId, false);
                retVal.SetProperties(properties);
            });

            return retVal;
        }

        public RevisionInternal PutLocalRevision(RevisionInternal revision, string prevRevId, bool obeyMVCC)
        {
            var docId = revision.GetDocId();
            if (!docId.StartsWith("_local/")) {
                throw new CouchbaseLiteException("Local revision IDs must start with _local/", StatusCode.BadId);
            }

            if (revision.IsDeleted()) {
                DeleteLocalRevision(docId, prevRevId, obeyMVCC);
                return revision;
            }

            var result = default(RevisionInternal);
            RunInTransaction(() =>
            {
                var json = Manager.GetObjectMapper().WriteValueAsString(revision.GetProperties(), true);
                WithC4Raw(docId, "_local", doc => 
                {
                    var generation = RevisionInternal.GenerationFromRevID(prevRevId);
                    if(obeyMVCC) {
                        if(prevRevId != null) {
                            if(prevRevId != (doc != null ? (string)doc->meta : null)) {
                                throw new CouchbaseLiteException(StatusCode.Conflict);
                            }

                            if(generation == 0) {
                                throw new CouchbaseLiteException(StatusCode.BadId);
                            }
                        } else if(doc != null) {
                            throw new CouchbaseLiteException(StatusCode.Conflict);
                        }
                    }

                    var newRevId = String.Format("{0}-local", ++generation);
                    ForestDBBridge.Check(err => Native.c4raw_put(_forest, "_local", docId, newRevId, json, err));
                    result = revision.CopyWithDocID(docId, newRevId);
                });

                return true;
            });

            return result;
        }

        public string GetInfo(string key)
        {
            var value = default(string);
            WithC4Raw(key, "info", doc =>
            {
                if(doc == null) {
                    return;
                }

                value = (string)doc->body;
            });

            return value;
        }

        public void SetInfo(string key, string info)
        {
            ForestDBBridge.Check(err => Native.c4raw_put(_forest, "info", key, null, info, err)); 
        }

        public RevisionInternal PutRevision(string inDocId, string inPrevRevId, IDictionary<string, object> properties,
            bool deleting, bool allowConflict, StoreValidation validationBlock)
        {
            if(_config.HasFlag(C4DatabaseFlags.ReadOnly)) {
                throw new CouchbaseLiteException("Attempting to write to a readonly database", StatusCode.Forbidden);
            }

            var json = default(IEnumerable<byte>);
            if (properties != null) {
                json = Manager.GetObjectMapper().WriteValueAsBytes(properties, true);
            } else {
                json = Encoding.UTF8.GetBytes("{}");
            }

            var putRev = default(RevisionInternal);
            var change = default(DocumentChange);
            RunInTransaction(() =>
            {
                var docId = inDocId;
                var prevRevId = inPrevRevId;
                WithC4Document(docId ?? Misc.CreateGUID(), null, false, doc =>
                {
                    if(prevRevId != null) {
                        // Updating an existing revision; make sure it exists and is a leaf:
                        ForestDBBridge.Check(err => Native.c4doc_selectRevision(doc, prevRevId, false, err));
                        if(!allowConflict && !doc->selectedRev.IsLeaf) {
                            throw new CouchbaseLiteException(StatusCode.Conflict);
                        }
                    } else {
                        // No parent revision given:
                        if(deleting) {
                            // Didn't specify a revision to delete: NotFound or a Conflict, depending
                            throw new CouchbaseLiteException(doc->Exists ? StatusCode.Conflict : StatusCode.NotFound);
                        }

                        // If doc exists, current rev must be in a deleted state or there will be a conflict:
                        if(Native.c4doc_selectCurrentRevision(doc)) {
                            if(doc->selectedRev.IsDeleted) {
                                // New rev will be child of the tombstone:
                                prevRevId = (string)doc->revID;
                            } else {
                                throw new CouchbaseLiteException(StatusCode.Conflict);
                            }
                        }
                    }

                    // Compute the new revID. (Can't be done earlier because prevRevID may have changed.)
                    var newRevID = Delegate != null ? Delegate.GenerateRevID(json, deleting, prevRevId) : null;
                    if(newRevID == null) {
                        throw new CouchbaseLiteException(StatusCode.BadId);
                    }

                    putRev = new RevisionInternal(docId, newRevID, deleting);
                    if(properties != null) {
                        properties["_id"] = docId;
                        properties["_rev"] = newRevID;
                        putRev.SetProperties(properties);
                    }

                    // Run any validation blocks:
                    if(validationBlock != null) {
                        var prevRev = default(RevisionInternal);
                        if(prevRevId != null) {
                            prevRev = new RevisionInternal(docId, prevRev, doc->selectedRev.IsDeleted);
                        }

                        try {
                            var status = validationBlock(putRev, prevRev, prevRevId);
                            if(status.IsError) {
                                return false;
                            }
                        } catch(Exception e) {
                            Log.W(TAG, "Exception throw in validation block", e);
                            return false;
                        }
                    }

                    // Add the revision to the database:
                    ForestDBBridge.Check(err => Native.c4doc_insertRevision(doc, newRevID, Encoding.UTF8.GetString(json), deleting,
                        putRev.GetAttachments() != null, allowConflict, err));
                    var isWinner = SaveDocument(doc, newRevID, properties);
                    putRev.SetSequence((long)doc->sequence);
                    change = ChangeWithNewRevision(putRev, isWinner, doc, null);
                });
            });

            if (Delegate != null) {
                Delegate.DatabaseStorageChanged(change);
            }

            return putRev;
        }

        public void ForceInsert(RevisionInternal inRev, IList<string> revHistory, StoreValidation validationBlock, Uri source)
        {
            if (_config.HasFlag(C4DatabaseFlags.ReadOnly)) {
                throw new CouchbaseLiteException("Attempting to write to a readonly database", StatusCode.Forbidden);
            }

            var json = Manager.GetObjectMapper().WriteValueAsBytes(inRev.GetProperties(), true);
            var change = default(DocumentChange);
            RunInTransaction(() =>
            {
                // First get the CBForest doc:
                WithC4Document(inRev.GetDocId(), null, false, doc =>
                {
                    ForestDBBridge.Check(err => Native.c4doc_insertRevisionWithHistory(doc, inRev.GetRevId(), json, inRev.IsDeleted(), 
                        inRev.GetAttachments() != null, revHistory.ToArray(), revHistory.Count, err));

                    // Save updated doc back to the database:
                    var isWinner = SaveDocument(doc, revHistory[0], inRev.GetProperties());
                    inRev.SetSequence((long)doc->sequence);
                    change = ChangeWithNewRevision(inRev, isWinner, doc, source);
                });
            });

            if (change != null && Delegate != null) {
                Delegate.DatabaseStorageChanged(change);
            }
        }

        public IViewStore GetViewStorage(string name, bool create)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetAllViews()
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    #endregion
}
#endif

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

    internal unsafe delegate int C4TryLogicDelegate3(C4Error *err);

    internal unsafe delegate bool C4RevisionSelector(C4Document *doc);

    #endregion

    #region ForestDBBridge

    internal unsafe static class ForestDBBridge {
        public static void Check(C4TryLogicDelegate1 block)
        {
            var err = default(C4Error);
            if (block(&err)) {
                return;
            }

            throw new CBForestException(err.code, err.domain);
        }

        public static void* Check(C4TryLogicDelegate2 block)
        {
            var err = default(C4Error);
            var obj = block(&err);
            if (obj != null) {
                return obj;
            }

            throw new CBForestException(err.code, err.domain);
        }

        public static void Check(C4TryLogicDelegate3 block)
        {
            var err = default(C4Error);
            var result = block(&err);
            if (result >= 0) {
                return;
            }

            throw new CBForestException(err.code, err.domain);
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

        private C4DatabaseFlags _config;
        private SymmetricKey _encryptionKey;
        private LruCache<string, ForestDBViewStore> _views = new LruCache<string, ForestDBViewStore>(100);

        #endregion

        #region Properties

        public SymmetricKey EncryptionKey
        {
            get {
                return _encryptionKey;
            }
        }

        public bool AutoCompact { get; set; }

        public int MaxRevTreeDepth { get; set; }

        public ICouchStoreDelegate Delegate { get; set; }

        public int DocumentCount
        {
            get {
                return (int)Native.c4db_getDocumentCount(Forest);
            }
        }

        public long LastSequence
        {
            get {
                return (long)Native.c4db_getLastSequence(Forest);
            }
        }

        public bool InTransaction
        {
            get {
                return Native.c4db_isInTransaction(Forest);
            }
        }

        public bool IsOpen
        { 
            get {
                return Forest != null;
            }
        }

        public string Directory { get; private set; }

        public C4Database* Forest { get; private set; }

        #endregion

        #region Constructors

        public ForestDBCouchStore()
        {
            AutoCompact = true;
            MaxRevTreeDepth = DEFAULT_MAX_REV_TREE_DEPTH;
        }

        #endregion

        #region Public Methods

        public RevisionInternal GetDocument(string docId, long sequence)
        {
            var retVal = default(RevisionInternal);
            WithC4Document(docId, sequence, doc =>
            {
                retVal = new RevisionInternal(doc, true);
            });

            return retVal;
        } 

        #endregion

        #region Internal Methods

        internal void ForgetStorage(string name)
        {
            _views.Remove(name);
        }

        #endregion

        #region Private Methods

        private void WithC4Document(string docId, string revId, bool withBody, bool create, C4DocumentActionDelegate block)
        {
            var doc = default(C4Document*);
            try {
                doc = (C4Document*)ForestDBBridge.Check(err => Native.c4doc_get(Forest, docId, !create, err));
                if(revId != null) {
                    ForestDBBridge.Check(err => Native.c4doc_selectRevision(doc, revId, withBody, err));
                } else if(withBody) {
                    ForestDBBridge.Check(err => Native.c4doc_loadRevisionBody(doc, err));
                }
            } catch(CBForestException e) {
                if (e.Domain != C4ErrorDomain.ForestDB || (fdb_status)e.Code != fdb_status.RESULT_KEY_NOT_FOUND) {
                    throw;
                }
            }

            try {
                block(doc);
            } finally {
                Native.c4doc_free(doc);
            }
        }

        private void WithC4Document(string docId, long sequence, C4DocumentActionDelegate block)
        {
            var doc = default(C4Document*);
            try {
                doc = (C4Document*)ForestDBBridge.Check(err => Native.c4doc_getBySequence(Forest, (ulong)sequence, err));
            } catch(CBForestException e) {
                if (e.Domain != C4ErrorDomain.ForestDB && (fdb_status)e.Code != fdb_status.RESULT_KEY_NOT_FOUND) {
                    throw;
                }
            }

            try {
                block(doc);
            } finally {
                Native.c4doc_free(doc);
            }
        }

        private void WithC4Raw(string docId, string storeName, C4RawDocumentActionDelegate block)
        {
            var doc = default(C4RawDocument*);
            try {
                doc = (C4RawDocument*)ForestDBBridge.Check(err => Native.c4raw_get(Forest, storeName, docId, err));
            } catch(CBForestException e) {
                if (e.Domain != C4ErrorDomain.ForestDB && (fdb_status)e.Code != fdb_status.RESULT_KEY_NOT_FOUND) {
                    throw;
                }
            }

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
                nativeKey = _encryptionKey.AsC4EncryptionKey();
            }
                
            var forestPath = Path.Combine(Directory, DB_FILENAME);
            var nativeKeyPtr = &nativeKey;
            Forest = (C4Database*)ForestDBBridge.Check(err => Native.c4db_open(forestPath, _config, nativeKeyPtr, err));
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
                var doc = (C4RawDocument *)ForestDBBridge.Check(err => Native.c4raw_get(Forest, "_local", docId, err));
                if(doc == null) {
                    throw new CouchbaseLiteException(StatusCode.NotFound);
                }

                if(obeyMVCC && (revId != (string)doc->meta)) {
                    throw new CouchbaseLiteException(StatusCode.Conflict);
                }

                ForestDBBridge.Check(err => Native.c4raw_put(Forest, "_local", docId, null, null, err));
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
            Directory = directory;
            if (!System.IO.Directory.Exists(directory)) {
                System.IO.Directory.CreateDirectory(Directory);
            }

            _config = readOnly ? C4DatabaseFlags.ReadOnly : C4DatabaseFlags.Create;
            if (AutoCompact) {
                _config |= C4DatabaseFlags.AutoCompact;
            }

            Reopen();
        }

        public void Close()
        {
            ForestDBBridge.Check(err => Native.c4db_close(Forest, err));
            Forest = null;
        }

        public void SetEncryptionKey(SymmetricKey key)
        {
            _encryptionKey = key;
        }

        public AtomicAction ActionToChangeEncryptionKey(SymmetricKey newKey)
        {
            return new AtomicAction(() =>
                ForestDBBridge.Check(err => 
                {
                    var newc4key = default(C4EncryptionKey);
                    if (newKey != null) {
                        newc4key = newKey.AsC4EncryptionKey();
                    }
                    return Native.c4db_rekey(Forest, &newc4key, err);
                }), null, null);
        }

        public void Compact()
        {
            ForestDBBridge.Check(err => Native.c4db_compact(Forest, err));
        }

        public bool RunInTransaction(RunInTransactionDelegate block)
        {
            Log.D(TAG, "BEGIN transaction...");
            ForestDBBridge.Check(err => Native.c4db_beginTransaction(Forest, err));
            var success = false;
            try {
                success = block();
            } catch(Exception e) {
                Log.E(TAG, "Exception in RunInTransaction block", e);
                success = false;
            }

            Log.D(TAG, "END transaction (success={0})", success);
            ForestDBBridge.Check(err => Native.c4db_endTransaction(Forest, success, err));
            if (!InTransaction && Delegate != null) {
                Delegate.StorageExitedTransaction(success);
            }

            return success;
        }

        public RevisionInternal GetDocument(string docId, string revId, bool withBody)
        {
            var retVal = default(RevisionInternal);
            WithC4Document(docId, revId, withBody, false, doc =>
            {
                if(doc == null) {
                    return;
                }

                retVal = new RevisionInternal(doc, withBody);
            });

            return retVal;
        }

        public void LoadRevisionBody(RevisionInternal rev)
        {
            WithC4Document(rev.GetDocId(), rev.GetRevId(), true, false, doc => rev.SetBody(new Body(doc->selectedRev.body)));
        }

        public long GetRevisionSequence(RevisionInternal rev)
        {
            var retVal = 0L;
            WithC4Document(rev.GetDocId(), rev.GetRevId(), false, false, doc => retVal = (long)doc->selectedRev.sequence);

            return retVal;
        }

        public RevisionInternal GetParentRevision(RevisionInternal rev)
        {
            var retVal = rev;
            WithC4Document(rev.GetDocId(), rev.GetRevId(), false, false, doc =>
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
            WithC4Document(docId, null, false, false, doc =>
            {
                retVal = new RevisionList(EnumerateHistory(new IntPtr(doc), true, onlyCurrent, false).ToList());
            });

            return retVal;
        }

        public IEnumerable<string> GetPossibleAncestors(RevisionInternal rev, int limit, bool onlyAttachments)
        {
            var generation = RevisionInternal.GenerationFromRevID(rev.GetRevId());
            if (generation <= 1) {
                yield break;
            }

            var returnedCount = 0;
            var doc = (C4Document*)ForestDBBridge.Check(err => Native.c4doc_get(Forest, rev.GetDocId(), true, err));
            var enumerator = new CBForestHistoryEnumerator(doc, false);
            foreach (var next in enumerator) {
                if(returnedCount >= limit) {
                    break;
                }

                var nextDoc = next.Document;
                var revId = (string)nextDoc->selectedRev.revID;
                if(RevisionInternal.GenerationFromRevID(revId) < generation &&
                    !nextDoc->selectedRev.IsDeleted && Native.c4doc_hasRevisionBody(nextDoc) &&
                    !(onlyAttachments && !nextDoc->selectedRev.HasAttachments)) {
                    returnedCount++;
                    yield return revId;
                }
            }
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
            WithC4Document(rev.GetDocId(), null, false, false, doc =>
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
            WithC4Document(rev.GetDocId(), null, false, false, doc =>
            {
                var newRev = new RevisionInternal(doc, false);
                while(!ancestorRevIds.Contains(newRev.GetRevId())) {
                    history.Add(newRev);
                    newRev.SetMissing(!Native.c4doc_hasRevisionBody(doc));
                    if(ancestorRevIds.Contains(newRev.GetRevId()) || !Native.c4doc_selectParentRevision(doc)) {
                        return;
                    }

                    newRev = new RevisionInternal(doc, false);
                } 
            });

            return history;
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
            var e = (C4DocEnumerator*)ForestDBBridge.Check(err => Native.c4db_enumerateChanges(Forest, (ulong)lastSequence, p, err));
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
            var forestOps = options.AsAllDocsOptions();
            var enumerator = new CBForestDocEnumerator(Forest, options.StartKeyDocId, options.EndKeyDocId, forestOps);

            foreach(var next in enumerator) {
                var doc = next.Document;
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
                        doc = (C4Document *)ForestDBBridge.Check(err => Native.c4doc_get(Forest, lastDocId, true, err));
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
                Native.c4db_enumerateAllDocs(Forest, null, null, p, err));
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
                    WithC4Document(docID, null, false, false, doc => {;
                        if(!doc->Exists) {
                            throw new CouchbaseLiteException(StatusCode.NotFound);
                        }

                        var revsPurged = default(IList<string>);
                        var revIDs = docRevPair.Value;
                        if(revIDs.Count == 0) {
                            revsPurged = new List<string>();
                        } else if(revIDs.Contains("*")) {
                            // Delete all revisions if magic "*" revision ID is given:
                            ForestDBBridge.Check(err => Native.c4db_purgeDoc(Forest, doc->docID, err));
                            revsPurged = new List<string> { "*" };
                            Log.D(TAG, "Purged document '{0}'", docID);
                        } else {
                            var purged = new List<string>();
                            foreach(var revID in revIDs) {
                                if(Native.c4doc_purgeRevision(doc, revID, null) > 0) {
                                    purged.Add(revID);
                                }
                            }

                            if(purged.Count > 0) {
                                ForestDBBridge.Check(err => Native.c4doc_save(doc, (uint)MaxRevTreeDepth, err));
                                Log.D(TAG, "Purged doc '{0}' revs {1}", docID, Manager.GetObjectMapper().WriteValueAsString(revIDs));
                            }

                            revsPurged = purged;
                        }

                        result[docID] = revsPurged;
                    });
                }

                return true;
            });

            return result;
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
                    ForestDBBridge.Check(err => Native.c4raw_put(Forest, "_local", docId, newRevId, json, err));
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
            ForestDBBridge.Check(err => Native.c4raw_put(Forest, "info", key, null, info, err)); 
        }

        public RevisionInternal PutRevision(string inDocId, string inPrevRevId, IDictionary<string, object> properties,
            bool deleting, bool allowConflict, StoreValidation validationBlock)
        {
            if(_config.HasFlag(C4DatabaseFlags.ReadOnly)) {
                throw new CouchbaseLiteException("Attempting to write to a readonly database", StatusCode.Forbidden);
            }

            var json = default(string);
            if (properties != null) {
                json = Manager.GetObjectMapper().WriteValueAsString(properties, true);
            } else {
                json = "{}";
            }

            var putRev = default(RevisionInternal);
            var change = default(DocumentChange);
            RunInTransaction(() =>
            {
                var docId = inDocId;
                var prevRevId = inPrevRevId;
                var transactionSuccess = false;
                WithC4Document(docId ?? Misc.CreateGUID(), null, false, true, doc =>
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
                    var newRevID = Delegate != null ? Delegate.GenerateRevID(Encoding.UTF8.GetBytes(json), deleting, prevRevId) : null;
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
                            prevRev = new RevisionInternal(docId, prevRevId, doc->selectedRev.IsDeleted);
                        }

                        try {
                            var status = validationBlock(putRev, prevRev, prevRevId);
                            if(status.IsError) {
                                transactionSuccess = false;
                            }
                        } catch(Exception e) {
                            Log.W(TAG, "Exception throw in validation block", e);
                            transactionSuccess = false;
                        }
                    }

                    // Add the revision to the database:
                    ForestDBBridge.Check(err => Native.c4doc_insertRevision(doc, newRevID, json, deleting,
                        putRev.GetAttachments() != null, allowConflict, err));
                    var isWinner = SaveDocument(doc, newRevID, properties);
                    putRev.SetSequence((long)doc->sequence);
                    change = ChangeWithNewRevision(putRev, isWinner, doc, null);
                    transactionSuccess = true;
                });

                return transactionSuccess;
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

            var json = Manager.GetObjectMapper().WriteValueAsString(inRev.GetProperties(), true);
            var change = default(DocumentChange);
            RunInTransaction(() =>
            {
                // First get the CBForest doc:
                WithC4Document(inRev.GetDocId(), null, false, true, doc =>
                {
                    ForestDBBridge.Check(err => Native.c4doc_insertRevisionWithHistory(doc, inRev.GetRevId(), json, inRev.IsDeleted(), 
                        inRev.GetAttachments() != null, revHistory.ToArray(), (uint)revHistory.Count, err) > 0);

                    // Save updated doc back to the database:
                    var isWinner = SaveDocument(doc, revHistory[0], inRev.GetProperties());
                    inRev.SetSequence((long)doc->sequence);
                    change = ChangeWithNewRevision(inRev, isWinner, doc, source);
                });

                return true;
            });

            if (change != null && Delegate != null) {
                Delegate.DatabaseStorageChanged(change);
            }
        }

        public IViewStore GetViewStorage(string name, bool create)
        {
            var view = _views[name];
            if (view == null) {
                try {
                    view = new ForestDBViewStore(this, name, create);
                    _views[name] = view;
                } catch(Exception e) {
                    Log.E(TAG, String.Format("Error creating view storage for {0}", name), e);
                    return null;
                }
            }

            return view;
        }

        public IEnumerable<string> GetAllViews()
        {
            return System.IO.Directory.EnumerateFiles(Directory).Select(x => ForestDBViewStore.FileNameToViewName(x));
        }

        #endregion
    }

    #endregion
}
#endif

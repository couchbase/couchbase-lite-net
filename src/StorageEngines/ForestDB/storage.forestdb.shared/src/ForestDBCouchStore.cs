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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using CBForest;
using Couchbase.Lite.Db;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Revisions;
using Couchbase.Lite.Storage.ForestDB.Internal;
using Couchbase.Lite.Store;
using Couchbase.Lite.Util;


namespace Couchbase.Lite.Storage.ForestDB
{

    #region Delegates

    internal unsafe delegate void C4DocumentActionDelegate(C4Document* doc);

    internal unsafe delegate void C4RawDocumentActionDelegate(C4RawDocument *doc);

    internal unsafe delegate bool C4RevisionSelector(C4Document *doc);

    #endregion

    #region ForestDBBridge

    internal unsafe static class ForestDBBridge {

        public static void Check(C4TryLogicDelegate1 block)
        {
            RetryHandler.RetryIfBusy().Execute(block);
        }

        public static void* Check(C4TryLogicDelegate2 block)
        {
            return RetryHandler.RetryIfBusy().Execute(block);
        }

        public static void Check(C4TryLogicDelegate3 block)
        {
            RetryHandler.RetryIfBusy().Execute(block);
        }
    }

    #endregion

    #region ForestDBCouchStore

    #if __IOS__
    [Foundation.Preserve(AllMembers = true)]
    #endif
    internal unsafe sealed class ForestDBCouchStore : ICouchStore
    {

        #region Constants

        private const int DEFAULT_MAX_REV_TREE_DEPTH = 20;
        private const string DB_FILENAME = "db.forest";
        private const string TAG = "ForestDBCouchStore";

        #endregion

        #region Variables

        private ConcurrentDictionary<int, IntPtr> _fdbConnections =
            new ConcurrentDictionary<int, IntPtr>();

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
            get;
            private set;
        }

        public string Directory { get; private set; }

        public C4Database* Forest 
        {
            get {
                if(!IsOpen) {
                    return null;
                }

                var threadId = Thread.CurrentThread.ManagedThreadId;

                var retVal = _fdbConnections.GetOrAdd(threadId, x => (IntPtr)Reopen());
                return (C4Database*)retVal.ToPointer();
            }
        }

        #endregion

        #region Constructors

        static ForestDBCouchStore()
        {
            Log.To.Database.I(TAG, "Initialized ForestDB store (version 'BETA' (55aed7836cbddc350309436895b406f85520f96c))");
            Native.c4log_register(C4LogLevel.Debug, (level, msg) =>
            {
                switch(level) {
                    case C4LogLevel.Debug:
                        Log.To.Database.V("ForestDB", msg);
                        break;
                    case C4LogLevel.Info:
                        Log.To.Database.I("ForestDB", msg);
                        break;
                    case C4LogLevel.Warning:
                        Log.To.Database.W("ForestDB", msg);
                        break;
                    case C4LogLevel.Error:
                        Log.To.Database.E("ForestDB", msg);
                        break;
                }
            });
        }

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
                Log.To.Database.D(TAG, "Read {0} seq {1}", docId, sequence);
                retVal = new ForestRevisionInternal(doc, true);
            });

            return retVal;
        } 

        #endregion

        #region Internal Methods

        internal void ForgetViewStorage(string name)
        {
            _views.Remove(name);
        }

        #endregion

        #region Private Methods

        private CBForestHistoryEnumerator GetHistoryFromSequence(long sequence)
        {
            return new CBForestHistoryEnumerator(Forest, sequence, true);
        }

        private long[] GetLastSequenceNumbers()
        {
            List<long> foo = new List<long>();
            foreach (var connection in _fdbConnections) {
                foo.Add((long)Native.c4db_getLastSequence((C4Database *)connection.Value.ToPointer()));
            }

            return foo.ToArray();
        }

        private bool[] GetIsInTransactions()
        {
            List<bool> foo = new List<bool>();
            foreach (var connection in _fdbConnections) {
                foo.Add(Native.c4db_isInTransaction((C4Database *)connection.Value.ToPointer()));
            }

            return foo.ToArray();
        }

        private CBForestDocEnumerator GetDocEnumerator(QueryOptions options, out List<string> remainingIDs)
        {
            var forestOps = options.AsC4EnumeratorOptions();
            var enumerator = default(CBForestDocEnumerator);
            remainingIDs = new List<string>();
            if(options.Keys != null) {
                try {
                    remainingIDs = options.Keys.Cast<string>().ToList();
                    enumerator = new CBForestDocEnumerator(Forest, remainingIDs.ToArray(), forestOps);
                } catch(InvalidCastException) {
                    Log.To.Database.E(TAG, "options.keys must contain strings");
                    throw;
                }
            } else {
                enumerator = new CBForestDocEnumerator(Forest, options.StartKey as string, options.EndKey as string, forestOps);
            }

            return enumerator;
        }

        private CBForestHistoryEnumerator GetHistoryEnumerator(RevisionInternal rev, int generation)
        {
            if(generation <= 1) {
                return null;
            }

            var doc = default(C4Document*);
            try {
                doc = (C4Document*)ForestDBBridge.Check(err => Native.c4doc_get(Forest, rev.DocID, true, err));
                ForestDBBridge.Check(err => Native.c4doc_selectCurrentRevision(doc));
            } catch(CBForestException e) {
                if(e.Domain == C4ErrorDomain.ForestDB && e.Code == (int)ForestDBStatus.KeyNotFound) {
                    return null;
                }

                throw;
            }

            return new CBForestHistoryEnumerator(doc, false, true);
        }

        private void WithC4Document(string docId, string revId, bool withBody, bool create, C4DocumentActionDelegate block)
        {
            var doc = default(C4Document*);
            try {
                doc = (C4Document*)ForestDBBridge.Check(err => Native.c4doc_get(Forest, docId, !create, err));
                if(revId != null) {
                    ForestDBBridge.Check(err => Native.c4doc_selectRevision(doc, revId, withBody, err));
                }

                if(withBody) {
                    ForestDBBridge.Check(err => Native.c4doc_loadRevisionBody(doc, err));
                }
            } catch(CBForestException e) {
                var is404 = e.Domain == C4ErrorDomain.ForestDB && e.Code == (int)ForestDBStatus.KeyNotFound;
                is404 |= e.Domain == C4ErrorDomain.HTTP && e.Code == 404;
                var is410 = e.Domain == C4ErrorDomain.HTTP && e.Code == 410; // Body compacted

                if (!is404 && !is410) {
                    throw;
                }

                Native.c4doc_free(doc); // In case the failure was in selectRevision
                doc = null;
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
                if (e.Domain != C4ErrorDomain.ForestDB && (ForestDBStatus)e.Code != ForestDBStatus.KeyNotFound) {
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
                if (e.Domain != C4ErrorDomain.ForestDB && (ForestDBStatus)e.Code != ForestDBStatus.KeyNotFound) {
                    throw;
                }
            }

            try {
                block(doc);
            } finally {
                Native.c4raw_free(doc);
            }
        }

        private C4Database* Reopen()
        {
            if (_encryptionKey != null) {
                Log.To.Database.I(TAG, "Database is encrypted; setting CBForest encryption key");
            }

            var forestPath = Path.Combine(Directory, DB_FILENAME);
            try {
                return (C4Database*)ForestDBBridge.Check(err => 
                {
                    var nativeKey = default(C4EncryptionKey);
                    if (_encryptionKey != null) {
                        nativeKey = new C4EncryptionKey(_encryptionKey.KeyData);
                    }

                    return Native.c4db_open(forestPath, _config, &nativeKey, err);
                });
            } catch(CBForestException e) {
                if (e.Domain == C4ErrorDomain.ForestDB && e.Code == (int)ForestDBStatus.NoDbHeaders) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Unauthorized, TAG,
                        "Failed to decrypt database, or it is corrupt");
                }

                Log.To.Database.E(TAG, "Got exception while opening database, rethrowing...");
                throw;
            }
        }

        private void DeleteLocalRevision(string docId, string revId, bool obeyMVCC)
        {
            if (!docId.StartsWith("_local/")) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadId, TAG,
                    "Local revision IDs must start with _local/");
            }

            if (obeyMVCC && revId == null) {
                // Didn't specify a revision to delete: NotFound or a Conflict, depending
                var gotLocalDoc = GetLocalDocument(docId, null);
                if(gotLocalDoc == null) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.NotFound, TAG,
                        "No revision ID specified in local delete operation");
                }

                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Conflict, TAG,
                    "No revision ID specified in local delete operation");
            }

            RunInTransaction(() =>
            {
                WithC4Raw(docId, "_local", doc =>
                {
                    if(doc == null) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.NotFound, TAG,
                            "Specified revision ({0}) in delete operation not found", revId);
                    }

                    var currentRevID = (string)doc->meta;
                    if(obeyMVCC && (revId != currentRevID)) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Conflict, TAG,
                            "Specified revision ({0}) in delete operation != current revision ({1})", revId, currentRevID);
                    }

                    ForestDBBridge.Check(err => Native.c4raw_put(Forest, "_local", docId, null, null, err));
                });
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
                var type = properties == null ? null : properties.GetCast<string>("type");
                Native.c4doc_setType(doc, type);
            }

            // Save:
            ForestDBBridge.Check(err => Native.c4doc_save(doc, (uint)MaxRevTreeDepth, err));
            return isWinner;
        }

        private DocumentChange ChangeWithNewRevision(RevisionInternal inRev, bool isWinningRev, C4Document *doc, Uri source)
        {
            var winningRevId = default(string);
            if(isWinningRev) {
                winningRevId = inRev.RevID;
            } else {
                winningRevId = (string)doc->revID;
            }

            return new DocumentChange(inRev, winningRevId, doc->IsConflicted, source);
        }

        private void SelectCurrentRevision(CBForestDocStatus status)
        {
            ForestDBBridge.Check(err => Native.c4doc_selectCurrentRevision(status.GetDocument()));
        }

        private void LoadRevisionBody(CBForestDocStatus status)
        {
            ForestDBBridge.Check(err => Native.c4doc_loadRevisionBody(status.GetDocument(), err));
        }

        private IDictionary<string, object> GetAllDocsEntry(string docId)
        {
            var value = default(IDictionary<string, object>);
            var existingDoc = default(C4Document*);
            try {
                existingDoc = (C4Document*)ForestDBBridge.Check(err => Native.c4doc_get(Forest, docId, true, err));
                if(existingDoc != null) {
                    value = new NonNullDictionary<string, object> {
                            { "rev", (string)existingDoc->revID },
                            { "deleted", true }
                        };
                }
            } catch(CBForestException e) {
                if(e.Domain != C4ErrorDomain.ForestDB || e.Code != (int)ForestDBStatus.KeyNotFound) {
                    throw;
                }
            } finally {
                Native.c4doc_free(existingDoc);
            }

            return value;
        }

        #endregion

        #region ICouchStore

        public IDatabaseUpgrader CreateUpgrader(Database upgradeTo, string upgradeFrom)
        {
            throw new NotSupportedException("Upgrades not supported on ForestDB");
        }

        public bool DatabaseExistsIn(string directory)
        {
            var dbPath = Path.Combine(directory, DB_FILENAME);
            return File.Exists(dbPath) || File.Exists(dbPath + ".meta"); // Auto-compaction changes the filename
        }

        public void Open(string directory, Manager manager, bool readOnly)
        {
            if(IsOpen) {
                return;
            }

            IsOpen = true;
            Directory = directory;
            if (!System.IO.Directory.Exists(directory)) {
                System.IO.Directory.CreateDirectory(Directory);
            }

            _config = readOnly ? C4DatabaseFlags.ReadOnly : C4DatabaseFlags.Create;
            if (AutoCompact) {
                _config |= C4DatabaseFlags.AutoCompact;
            }

            _fdbConnections.GetOrAdd(Thread.CurrentThread.ManagedThreadId, x => (IntPtr)Reopen());
        }

        public void Close()
        {
            IsOpen = false;
            var connections = _fdbConnections;
            _fdbConnections = new ConcurrentDictionary<int, IntPtr>();
            foreach(var ptr in connections) {
                ForestDBBridge.Check(err => Native.c4db_close((C4Database*)ptr.Value.ToPointer(), err));
            }
        }

        public void SetEncryptionKey(SymmetricKey key)
        {
            _encryptionKey = key;
        }

        public AtomicAction ActionToChangeEncryptionKey(SymmetricKey newKey)
        {
            var retVal = new AtomicAction(() =>
                ForestDBBridge.Check(err => 
                {
                    var newc4key = default(C4EncryptionKey);
                    if (newKey != null) {
                        newc4key = new C4EncryptionKey(newKey.KeyData);
                    }

                    return Native.c4db_rekey(Forest, &newc4key, err);
                }), null, null);

            foreach (var viewName in GetAllViews()) {
                var store = GetViewStorage(viewName, false) as ForestDBViewStore;
                if (store == null) {
                    continue;
                }

                retVal.AddLogic(store.ActionToChangeEncryptionKey(newKey));
            }

            return retVal;
        }

        public void Compact()
        {
            ForestDBBridge.Check(err => Native.c4db_compact(Forest, err));
        }

        public bool RunInTransaction(RunInTransactionDelegate block)
        {
            Log.To.Database.I(TAG, "BEGIN transaction...");
            ForestDBBridge.Check(err => Native.c4db_beginTransaction(Forest, err));
            var success = false;
            try {
                success = block();
            } catch(CouchbaseLiteException) {
                Log.To.Database.W(TAG, "Failed to run transaction");
                success = false;
                throw;
            } catch(Exception e) {
                success = false;
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, "Error running transaction");
            } finally {
                Log.To.Database.I(TAG, "END transaction (success={0})", success);
                ForestDBBridge.Check(err => Native.c4db_endTransaction(Forest, success, err));
                if (!InTransaction && Delegate != null) {
                    Delegate.StorageExitedTransaction(success);
                }
            }

            return success;
        }

        public RevisionInternal GetDocument(string docId, string revId, bool withBody, Status outStatus = null)
        {
            if (outStatus == null) {
                outStatus = new Status();
            }

            var retVal = default(RevisionInternal);
            WithC4Document(docId, revId, withBody, false, doc =>
            {
                Log.To.Database.D(TAG, "Read {0} rev {1}", docId, revId);
                if(doc == null) {
                    outStatus.Code = StatusCode.NotFound;
                    return;
                }

                if(revId == null && doc->IsDeleted) {
                    outStatus.Code = revId == null ? StatusCode.Deleted : StatusCode.NotFound;
                    return;
                }

                outStatus.Code = StatusCode.Ok;
                retVal = new ForestRevisionInternal(doc, withBody);
            });

            return retVal;
        }

        public void LoadRevisionBody(RevisionInternal rev)
        {
            WithC4Document(rev.DocID, rev.RevID, true, false, doc => 
            {
                if(doc == null) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.NotFound, TAG, 
                        "Cannot load revision body for non-existent revision {0}", rev);
                }

                rev.SetBody(new Body(doc->selectedRev.body));
            });
        }

        public long GetRevisionSequence(RevisionInternal rev)
        {
            var retVal = 0L;
            WithC4Document(rev.DocID, rev.RevID, false, false, doc => retVal = (long)doc->selectedRev.sequence);

            return retVal;
        }

        public RevisionInternal GetParentRevision(RevisionInternal rev)
        {
            var retVal = default(RevisionInternal);
            WithC4Document(rev.DocID, rev.RevID, false, false, doc =>
            {
                if (!Native.c4doc_selectParentRevision(doc)) {
                    return;
                }
                    
                ForestDBBridge.Check(err => Native.c4doc_loadRevisionBody(doc, err));
                retVal = new RevisionInternal((string)doc->docID, (string)doc->selectedRev.revID, doc->selectedRev.IsDeleted);
                retVal.Sequence = (long)doc->selectedRev.sequence;
                retVal.SetBody(new Body(doc->selectedRev.body));
            });

            return retVal;
        }

        public RevisionList GetAllDocumentRevisions(string docId, bool onlyCurrent)
        {
            var retVal = default(RevisionList);
            WithC4Document(docId, null, false, false, doc =>
            {
                using(var enumerator = new CBForestHistoryEnumerator(doc, onlyCurrent, false)) {
                    retVal = new RevisionList(enumerator.Select(x => new ForestRevisionInternal(x.GetDocument(), false)).Cast<RevisionInternal>().ToList());
                }
            });

            return retVal;
        }

        public IEnumerable<string> GetPossibleAncestors(RevisionInternal rev, int limit, bool onlyAttachments)
        {
            var returnedCount = 0;
            var generation = RevisionID.GetGeneration(rev.RevID);
            var enumerator = GetHistoryEnumerator(rev, generation);
            if(enumerator == null) {
                yield break;
            }

            foreach (var next in enumerator) {
                if(returnedCount >= limit) {
                    break;
                }

                var revId = next.CurrentRevID;
                if(RevisionID.GetGeneration(revId) < generation &&
                    !next.SelectedRev.IsDeleted && next.HasRevisionBody &&
                    !(onlyAttachments && !next.SelectedRev.HasAttachments)) {
                    returnedCount++;
                    yield return revId;
                }
            }
        }

        public string FindCommonAncestor(RevisionInternal rev, IEnumerable<string> revIds)
        {
            var generation = RevisionID.GetGeneration(rev.RevID);
            var revIdArray = revIds == null ? null : revIds.ToList();
            if (generation <= 1 || revIdArray == null || revIdArray.Count == 0) {
                return null;
            }
             
            revIdArray.Sort(RevisionID.CBLCompareRevIDs);
            var commonAncestor = default(string);
            WithC4Document(rev.DocID, null, false, false, doc =>
            {
                foreach(var possibleRevId in revIds) {
                    if(RevisionID.GetGeneration(possibleRevId) <= generation &&
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
            var history = new List<RevisionInternal>();
            WithC4Document(rev.DocID, rev.RevID, false, false, doc =>
            {
                var enumerator = new CBForestHistoryEnumerator(doc, false);
                foreach(var next in enumerator) {
                    var newRev = new ForestRevisionInternal(next.GetDocument(), false);
                    newRev.Missing = !Native.c4doc_hasRevisionBody(next.GetDocument());
                    history.Add(newRev);

                    if(ancestorRevIds != null && ancestorRevIds.Contains((string)next.SelectedRev.revID)) {
                        break;
                    }
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
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.NotImplemented, TAG,
                    "Descending ChangesSince is not currently implemented " +
                    "(see https://github.com/couchbase/couchbase-lite-ios/issues/641)");
            }

            var forestOps = C4EnumeratorOptions.DEFAULT;
            forestOps.flags |= C4EnumeratorFlags.IncludeDeleted | C4EnumeratorFlags.IncludeNonConflicted;
            if (options.IncludeDocs || options.IncludeConflicts || filter != null) {
                forestOps.flags |= C4EnumeratorFlags.IncludeBodies;
            }

            var changes = new RevisionList();
            var e = new CBForestDocEnumerator(Forest, lastSequence, forestOps);
            foreach (var next in e) {
                var revs = default(IEnumerable<RevisionInternal>);
                if (options.IncludeConflicts) {
                    using (var enumerator = new CBForestHistoryEnumerator(next.GetDocument(), true, false)) {
                        var includeBody = forestOps.flags.HasFlag(C4EnumeratorFlags.IncludeBodies);
                        revs = enumerator.Select<CBForestDocStatus, RevisionInternal>(x => new ForestRevisionInternal(x.GetDocument(), includeBody)).ToList();
                    }
                } else {
                    revs = new List<RevisionInternal> { new ForestRevisionInternal(next.GetDocument(), forestOps.flags.HasFlag(C4EnumeratorFlags.IncludeBodies)) };
                }

                foreach (var rev in revs) {
                    Debug.Assert(rev != null);
                    if (filter == null || filter(rev)) {
                        if (!options.IncludeDocs) {
                            rev.SetBody(null);
                        }

                        if(filter == null || filter(rev)) {
                            changes.Add(rev);
                        }
                    }
                }
            }

            if (options.SortBySequence) {
                changes.SortBySequence(!options.Descending);
                changes.Limit(options.Limit);
            }

            return changes;
        }

        public IEnumerable<QueryRow> GetAllDocs(QueryOptions options)
        {
            var remainingIDs = default(List<string>);
            var enumerator = GetDocEnumerator(options, out remainingIDs);
            var current = 0;
            foreach(var next in enumerator) {
                if (current++ >= options.Limit) {
                    yield break;
                }

                var sequenceNumber = 0L;
                var docID = next.CurrentDocID;
                remainingIDs.Remove(docID);
                var value = default(IDictionary<string, object>);
                if (next.Exists) {
                    sequenceNumber = (long)next.SelectedRev.sequence;
                    var conflicts = default(IList<string>);
                    if (options.AllDocsMode >= AllDocsMode.ShowConflicts && next.IsConflicted) {
                        SelectCurrentRevision(next);
                        LoadRevisionBody(next);
                        using (var innerEnumerator = GetHistoryFromSequence(next.Sequence)) {
                            conflicts = innerEnumerator.Select(x => (string)x.SelectedRev.revID).ToList();
                        }

                        if (conflicts.Count == 1) {
                            conflicts = null;
                        }
                    }

                    bool valid = conflicts != null || options.AllDocsMode != AllDocsMode.OnlyConflicts;
                    if (!valid) {
                        continue;
                    }

                    value = new NonNullDictionary<string, object> {
                        { "rev", next.CurrentRevID },
                        { "deleted", next.IsDeleted ? (object)true : null },
                        { "_conflicts", conflicts }
                    };
                    Log.To.Query.V(TAG, "AllDocs: Found row with key=\"{0}\", value={1}",
                        new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure), 
                        new SecureLogJsonString(value, LogMessageSensitivity.PotentiallyInsecure));
                } else {
                    Log.To.Query.V(TAG, "AllDocs: No such row with key=\"{0}\"", new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure));
                }

                var row = new QueryRow(value == null ? null : docID, sequenceNumber, docID, value, 
                    value == null ? null : new ForestRevisionInternal(next, options.IncludeDocs), null);
                if (options.Filter == null || options.Filter(row)) {
                    yield return row;
                } else {
                    Log.To.Query.V(TAG, "   ... on 2nd thought, filter predicate skipped that row");
                }
            }

            foreach (var docId in remainingIDs) {
                var value = GetAllDocsEntry(docId);
                
                    
                var row = new QueryRow(value != null ? docId as string : null, 0, docId, value, null, null);
                if (options.Filter == null || options.Filter(row)) {
                    yield return row;
                }
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
                    if (rev.DocID != lastDocId) {
                        lastDocId = rev.DocID;
                        Native.c4doc_free(doc);
                        doc = Native.c4doc_get(Forest, lastDocId, true, null);
                    }

                    if(doc == null) {
                        continue;
                    }

                    if (Native.c4doc_selectRevision(doc, rev.RevID, false, null)) {
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
            var options = C4EnumeratorOptions.DEFAULT;
            options.flags &= ~C4EnumeratorFlags.IncludeBodies;
            options.flags |= C4EnumeratorFlags.IncludeDeleted;
            var e = new CBForestDocEnumerator(Forest, null, null, options);
            foreach(var next in e) {
                var docInfo = next.DocumentInfo;
                if (!docInfo->HasAttachments || (docInfo->IsDeleted && !docInfo->IsConflicted)) {
                    continue;
                }

                var doc = next.GetDocument();
                // Since db is assumed to have just been compacted, we know that non-current revisions
                // won't have any bodies. So only scan the current revs.
                do {
                    if(doc->selectedRev.IsActive && doc->selectedRev.HasAttachments) {
                        ForestDBBridge.Check(err => Native.c4doc_loadRevisionBody(doc, err));
                        var body = doc->selectedRev.body;
                        if(body.size > 0) {
                            var rev = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(body);
                            foreach(var entry in rev.Get("_attachments").AsDictionary<string, IDictionary<string, object>>()) {
                                try {
                                    var key = new BlobKey(entry.Value.GetCast<string>("digest"));
                                    keys.Add(key);
                                } catch(Exception){
                                    Log.To.Database.W(TAG, "Invalid digest {0}; skipping", entry.Value.GetCast<string>("digest"));
                                }
                            }
                        }
                    }
                } while(Native.c4doc_selectNextLeafRevision(doc, true, true, null));
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

            Log.To.Database.I(TAG, "Purging {0} docs...", docsToRev.Count);
            RunInTransaction(() =>
            {
                foreach(var docRevPair in docsToRev) {
                    var docID = docRevPair.Key;
                    WithC4Document(docID, null, false, false, doc => {;
                        if(!doc->Exists) {
                            throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.NotFound, TAG,
                                "Invalid attempt to purge revisions of a nonexistent document (ID={0})",
                                new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure));
                        }
                        var revsPurged = default(IList<string>);
                        var revIDs = docRevPair.Value;
                        if(revIDs.Count == 0) {
                            revsPurged = new List<string>();
                        } else if(revIDs.Contains("*")) {
                            // Delete all revisions if magic "*" revision ID is given:
                            ForestDBBridge.Check(err => Native.c4db_purgeDoc(Forest, doc->docID, err));
                            revsPurged = new List<string> { "*" };
                            Log.To.Database.I(TAG, "Purged document '{0}'", new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure));
                        } else {
                            var purged = new List<string>();
                            foreach(var revID in revIDs) {
                                if(Native.c4doc_purgeRevision(doc, revID, null) > 0) {
                                    purged.Add(revID);
                                }
                            }

                            if(purged.Count > 0) {
                                ForestDBBridge.Check(err => Native.c4doc_save(doc, (uint)MaxRevTreeDepth, err));
                                Log.To.Database.I(TAG, "Purged doc '{0}' revs {1}", 
                                    new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure),
                                    new LogJsonString(revIDs));
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
                if(revId != null && revId != gotRevId || doc->body.size == 0) {
                    return;
                }

                var properties = default(IDictionary<string, object>);
                try {
                    properties = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(doc->body);
                } catch(CouchbaseLiteException) {
                    Log.To.Database.W(TAG, "Invalid JSON for document {0}\n{1}", 
                        new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure),
                        new SecureLogString(doc->body.ToArray(), LogMessageSensitivity.PotentiallyInsecure));
                    return;
                }

                properties["_id"] = docId;
                properties["_rev"] = gotRevId;
                retVal = new RevisionInternal(docId, revId, false);
                retVal.SetProperties(properties);
            });

            return retVal;
        }

        public RevisionInternal PutLocalRevision(RevisionInternal revision, string prevRevId, bool obeyMVCC)
        {
            var docId = revision.DocID;
            if (!docId.StartsWith("_local/")) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadId, TAG,
                    "Invalid document ID ({0}) in write operation, it must start with _local/", 
                    new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
            }

            if (revision.Deleted) {
                DeleteLocalRevision(docId, prevRevId, obeyMVCC);
                return revision;
            }

            var result = default(RevisionInternal);
            RunInTransaction(() =>
            {
                var json = Manager.GetObjectMapper().WriteValueAsString(revision.GetProperties(), true);
                WithC4Raw(docId, "_local", doc => 
                {
                    var generation = RevisionID.GetGeneration(prevRevId);
                    if(obeyMVCC) {
                        var currentRevId = (doc != null ? (string)doc->meta : null);
                        if(prevRevId != null) {
                            if(prevRevId != currentRevId) {
                                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Conflict, TAG,
                                    "Attempt to write new revision on {0} of {1} when a newer revision ({2}) exists",
                                    prevRevId, new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure),
                                    currentRevId);
                            }

                            if(generation == 0) {
                                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadId, TAG,
                                    "Attempt to write new revision on invalid revision ID ({0}) for document {1}",
                                    prevRevId, new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                            }
                        } else if(doc != null) {
                            throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Conflict, TAG,
                                "Revision ID not specified, but document {0} already exists (current rev: {1})",
                                new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure), currentRevId);
                        }
                    }

                    var newRevId = String.Format("{0}-local", ++generation);
                    ForestDBBridge.Check(err => Native.c4raw_put(Forest, "_local", docId, newRevId, json, err));
                    result = revision.Copy(docId, newRevId);
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
            bool deleting, bool allowConflict, Uri source, StoreValidation validationBlock)
        {
            if(_config.HasFlag(C4DatabaseFlags.ReadOnly)) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Forbidden, TAG,
                    "Attempting to write to a readonly database (PutRevision)");
            }

            var json = default(string);
            if (properties != null) {
                json = Manager.GetObjectMapper().WriteValueAsString(Database.StripDocumentJSON(properties), true);
            } else {
                json = "{}";
            }

            if (inDocId == null) {
                inDocId = Misc.CreateGUID();
            }

            var putRev = default(RevisionInternal);
            var change = default(DocumentChange);
            var success = RunInTransaction(() =>
            {
                var docId = inDocId;
                var prevRevId = inPrevRevId;
                var transactionSuccess = false;
                WithC4Document(docId, null, false, true, doc =>
                {
                    if(prevRevId != null) {
                        // Updating an existing revision; make sure it exists and is a leaf:
                        ForestDBBridge.Check(err => Native.c4doc_selectRevision(doc, prevRevId, false, err));
                        if(!allowConflict && !doc->selectedRev.IsLeaf) {
                            throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Conflict, TAG,
                                "Invalid attempt to create a conflict (allowConflict == false) on document " +
                                "{0} revision {1} (current revision {2})", 
                                new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure),
                                prevRevId, (string)doc->revID);
                        }
                    } else {
                        // No parent revision given:
                        if(deleting) {
                            // Didn't specify a revision to delete: NotFound or a Conflict, depending
                            var status = doc->Exists ? StatusCode.Conflict : StatusCode.NotFound;
                            throw Misc.CreateExceptionAndLog(Log.To.Database, status, TAG,
                                "No revision ID specified for delete operation on {0}",
                                new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
                        }

                        // If doc exists, current rev must be in a deleted state or there will be a conflict:
                        if(Native.c4doc_selectCurrentRevision(doc)) {
                            if(doc->selectedRev.IsDeleted) {
                                // New rev will be child of the tombstone:
                                prevRevId = (string)doc->revID;
                            } else {
                                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Conflict, TAG,
                                    "No revision ID specified, and {0} already has a non-deleted current revision ({1})",
                                    new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure),
                                    (string)doc->revID);
                            }
                        }
                    }

                    // Compute the new revID. (Can't be done earlier because prevRevID may have changed.)
                    var newRevID = Delegate != null ? Delegate.GenerateRevID(Encoding.UTF8.GetBytes(json), deleting, prevRevId) : null;
                    if(newRevID == null) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadId, TAG,
                            "Unable to generate new revision ID (previous one ({0}) is invalid?)", prevRevId);
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
                            
                        var status = validationBlock(putRev, prevRev, prevRevId);
                        if(status.IsError) {
                            throw Misc.CreateExceptionAndLog(Log.To.Validation, status.Code, TAG,
                                "{0} ({1}) failed validation",
                                new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure),
                                newRevID);
                        }
                    }

                    // Add the revision to the database:
                    ForestDBBridge.Check(err => Native.c4doc_insertRevision(doc, newRevID, json, deleting,
                        putRev.GetAttachments() != null, allowConflict, err));
                    var isWinner = SaveDocument(doc, newRevID, properties);
                    putRev.Sequence = (long)doc->sequence;
                    Log.To.Database.D(TAG, "Saved {0}", docId);
                    change = ChangeWithNewRevision(putRev, isWinner, doc, source);
                    transactionSuccess = true;
                });

                return transactionSuccess;
            });

            if (!success) {
                return null;
            }

            if (Delegate != null && change != null) {
                Delegate.DatabaseStorageChanged(change);
            }

            return putRev;
        }

        public void ForceInsert(RevisionInternal inRev, IList<string> revHistory, StoreValidation validationBlock, Uri source)
        {
            if(_config.HasFlag(C4DatabaseFlags.ReadOnly)) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Forbidden, TAG,
                    "Attempting to write to a readonly database (ForceInsert)");
            }

            var json = Manager.GetObjectMapper().WriteValueAsString(inRev.GetProperties(), true);
            var change = default(DocumentChange);
            RunInTransaction(() =>
            {
                // First get the CBForest doc:
                WithC4Document(inRev.DocID, null, false, true, doc =>
                {
                    ForestDBBridge.Check(err => Native.c4doc_insertRevisionWithHistory(doc, json, inRev.Deleted, 
                        inRev.GetAttachments() != null, revHistory.ToArray(), err));

                    // Save updated doc back to the database:
                    var isWinner = SaveDocument(doc, revHistory[0], inRev.GetProperties());
                    inRev.Sequence = (long)doc->sequence;
                    Log.To.Database.D(TAG, "Saved {0}", inRev.DocID);
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
                } catch(InvalidOperationException) {
                    return null;
                } catch(Exception e) {
                    Log.To.View.W(TAG, String.Format("Error creating view storage for {0}, returning null...", name), e);
                    return null;
                }
            }

            return view;
        }

        public IEnumerable<string> GetAllViews()
        {
            return System.IO.Directory.GetFiles(Directory, "*."+ForestDBViewStore.VIEW_INDEX_PATH_EXTENSION).
                Select(x => ForestDBViewStore.FileNameToViewName(Path.GetFileName(x)));
        }

        #endregion
    }

    #endregion
}

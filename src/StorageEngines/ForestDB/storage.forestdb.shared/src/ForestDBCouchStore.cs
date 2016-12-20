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
    /// <summary>
    /// This class will register this storage engine for use with Couchbase Lite
    /// </summary>
    public static class Plugin
    {
        /// <summary>
        /// Register this class for use as the storage engine for the ForestDB storage type
        /// (be careful, once you set this you cannot change it)
        /// </summary>
        public static void Register()
        {
            Database.RegisterStorageEngine(StorageEngineTypes.ForestDB, typeof(ForestDBCouchStore));
        }
    }

    #region Delegates

    internal unsafe delegate void C4DocumentActionDelegate(C4Document* doc);

    internal unsafe delegate void C4RawDocumentActionDelegate(C4RawDocument* doc);

    internal unsafe delegate bool C4RevisionSelector(C4Document* doc);

    #endregion

    #region ForestDBBridge

    internal unsafe static class ForestDBBridge
    {

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
            Native.c4log_register(C4LogLevel.Debug, (level, msg) =>
            {
                switch(level) {
                    case C4LogLevel.Debug:
                        Log.To.Database.D("ForestDB", msg);
                        break;
                    case C4LogLevel.Info:
                        Log.To.Database.V("ForestDB", msg);
                        break;
                    case C4LogLevel.Warning:
                        Log.To.Database.W("ForestDB", msg);
                        break;
                    case C4LogLevel.Error:
                        Log.To.Database.E("ForestDB", msg);
                        break;
                }
            });

            try {
                Native.c4doc_generateOldStyleRevID(true);
            } catch {
                Log.W(TAG, "Out of date ForestDB native binaries detected!");
            }
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
            foreach(var connection in _fdbConnections) {
                foo.Add((long)Native.c4db_getLastSequence((C4Database*)connection.Value.ToPointer()));
            }

            return foo.ToArray();
        }

        private bool[] GetIsInTransactions()
        {
            List<bool> foo = new List<bool>();
            foreach(var connection in _fdbConnections) {
                foo.Add(Native.c4db_isInTransaction((C4Database*)connection.Value.ToPointer()));
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
                string startKey, endKey;
                if(options.Descending) {
                    startKey = Misc.KeyForPrefixMatch(options.StartKey, options.PrefixMatchLevel) as string;
                    endKey = options.EndKey as string;
                } else {
                    startKey = options.StartKey as string;
                    endKey = Misc.KeyForPrefixMatch(options.EndKey, options.PrefixMatchLevel) as string;
                }
                enumerator = new CBForestDocEnumerator(Forest, startKey, endKey, forestOps);
            }

            return enumerator;
        }

        private CBForestHistoryEnumerator GetHistoryEnumerator(RevisionInternal rev, int generation, bool onlyCurrent = false)
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

            return new CBForestHistoryEnumerator(doc, onlyCurrent, true);
        }

        private void WithC4Document(string docId, RevisionID revId, bool withBody, bool create, C4DocumentActionDelegate block)
        {
            if(!IsOpen) {
                return;
            }

            var doc = default(C4Document*);
            try { 
                doc = (C4Document *)RetryHandler.RetryIfBusy().AllowErrors(
                    new C4Error() { code = 404, domain = C4ErrorDomain.HTTP },
                    new C4Error() { code = (int)ForestDBStatus.KeyNotFound, domain = C4ErrorDomain.ForestDB })
                    .Execute(err => Native.c4doc_get(Forest, docId, !create, err));
                if(doc != null) {
                    var selected = true;
                    if(revId != null) {
                        selected = RetryHandler.RetryIfBusy().HandleExceptions(e =>
                        {
                            if(e.Code == 404) {
                                Native.c4doc_free(doc);
                                doc = null;
                                return;
                            }

                            throw e;
                        }).AllowError(410, C4ErrorDomain.HTTP).Execute(err =>
                        {
                            bool result = false;
                            revId.PinAndUse(slice =>
                            {
                                result = Native.c4doc_selectRevision(doc, slice, withBody, err);
                            });

                            return result;
                        });
                    }

                    if(selected && withBody) {
                        RetryHandler.RetryIfBusy().AllowError(410, C4ErrorDomain.HTTP).Execute((err => Native.c4doc_loadRevisionBody(doc, err)));
                    }
                }

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
                if(e.Domain != C4ErrorDomain.ForestDB && (ForestDBStatus)e.Code != ForestDBStatus.KeyNotFound) {
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
                if(e.Domain != C4ErrorDomain.ForestDB && (ForestDBStatus)e.Code != ForestDBStatus.KeyNotFound) {
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
            if(_encryptionKey != null) {
                Log.To.Database.I(TAG, "Database is encrypted; setting CBForest encryption key");
            }

            var forestPath = Path.Combine(Directory, DB_FILENAME);
            try {
                return (C4Database*)ForestDBBridge.Check(err =>
                {
                    var nativeKey = default(C4EncryptionKey);
                    if(_encryptionKey != null) {
                        nativeKey = new C4EncryptionKey(_encryptionKey.KeyData);
                    }

                    return Native.c4db_open(forestPath, _config, &nativeKey, err);
                });
            } catch(CBForestException e) {
                if(e.Domain == C4ErrorDomain.ForestDB && e.Code == (int)ForestDBStatus.NoDbHeaders) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Unauthorized, TAG,
                        "Failed to decrypt database, or it is corrupt");
                }

                Log.To.Database.E(TAG, "Got exception while opening database, rethrowing...");
                throw;
            }
        }

        private void DeleteLocalRevision(string docId, RevisionID revId, bool obeyMVCC)
        {
            if(!docId.StartsWith("_local/")) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadId, TAG,
                    "Local revision IDs must start with _local/");
            }

            if(obeyMVCC && revId == null) {
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

                    var currentRevID = doc->meta.AsRevID();
                    if(obeyMVCC && (revId != currentRevID)) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Conflict, TAG,
                            "Specified revision ({0}) in delete operation != current revision ({1})", revId, currentRevID);
                    }

                    ForestDBBridge.Check(err => Native.c4raw_put(Forest, "_local", docId, null, null, err));
                });
                return true;
            });
        }

        private bool SaveDocument(C4Document* doc, RevisionID revId, IDictionary<string, object> properties)
        {
            // Is the new revision the winner?
            var winningRevID = doc->revID.AsRevID();
            bool isWinner = winningRevID.Equals(revId);

            // Update the documentType:
            if(!isWinner) {
                Native.c4doc_selectCurrentRevision(doc);
                properties = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(doc->selectedRev.body);
            }

            Native.c4doc_setType(doc, properties?.GetCast<string>("type"));
            // Save:
            ForestDBBridge.Check(err => Native.c4doc_save(doc, (uint)MaxRevTreeDepth, err));
            return isWinner;
        }

        private DocumentChange ChangeWithNewRevision(RevisionInternal inRev, bool isWinningRev, C4Document* doc, Uri source)
        {
            var winningRevId = default(RevisionID);
            if(isWinningRev) {
                winningRevId = inRev.RevID;
            } else {
                winningRevId = doc->revID.AsRevID();
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
            if(!System.IO.Directory.Exists(directory)) {
                System.IO.Directory.CreateDirectory(Directory);
            }

            _config = readOnly ? C4DatabaseFlags.ReadOnly : C4DatabaseFlags.Create;
            if(AutoCompact) {
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
                if(!Native.c4db_isInTransaction((C4Database*)ptr.Value)) {
                    ForestDBBridge.Check(err => Native.c4db_close((C4Database*)ptr.Value.ToPointer(), err));
                    Native.c4db_free((C4Database*)ptr.Value.ToPointer());
                }
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
                    if(newKey != null) {
                        newc4key = new C4EncryptionKey(newKey.KeyData);
                    }

                    return Native.c4db_rekey(Forest, &newc4key, err);
                }), null, null);

            foreach(var viewName in GetAllViews()) {
                var store = GetViewStorage(viewName, false) as ForestDBViewStore;
                if(store == null) {
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
            var nativeDb = Forest;
            if(nativeDb == null) {
                Log.To.Database.W(TAG, "RunInTransaction called on a closed database, returning false...");
                return false;
            }

            Log.To.Database.V(TAG, "BEGIN transaction...");
            try {
                ForestDBBridge.Check(err => Native.c4db_beginTransaction(nativeDb, err));
            } catch(CBForestException e) {
                if(e.Code == (int)ForestDBStatus.InvalidHandle) {
                    // Database was closed between the start of the method and now
                    Log.To.Database.W(TAG, "RunInTransaction called on a closed database, caught InvalidHandle and returning false...");
                    return false;
                }

                throw;
            }

            // At this point we can rest assured that the connection won't be closed from under us
            var success = false;
            try {
                success = block();
            } catch(CouchbaseLiteException) {
                Log.To.Database.W(TAG, "Failed to run transaction");
                success = false;
                throw;
            } catch(CBForestException e) {
                success = false;
                if(e.Domain == C4ErrorDomain.HTTP) {
                    var code = e.Code;
                    throw Misc.CreateExceptionAndLog(Log.To.Database, (StatusCode)code, TAG, "Failed to run transaction");
                }

                throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, "Error running transaction");
            } catch(Exception e) {
                success = false;
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, "Error running transaction");
            } finally {
                Log.To.Database.V(TAG, "END transaction (success={0})", success);
                ForestDBBridge.Check(err => Native.c4db_endTransaction(nativeDb, success, err));
                if(!InTransaction && Delegate != null) {
                    Delegate.StorageExitedTransaction(success);
                    if(!IsOpen) {
                        ForestDBBridge.Check(err => Native.c4db_close(nativeDb, err));
                        Native.c4db_free(nativeDb);
                    }
                }
            }

            return success;
        }

        public RevisionInternal GetDocument(string docId, RevisionID revId, bool withBody, Status outStatus = null)
        {
            if(outStatus == null) {
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

        public DateTime? NextDocumentExpiry()
        {
            var timestamp = IsOpen ? Native.c4db_nextDocExpiration(Forest) : 0UL;
            if(timestamp == 0UL) {
                return null;
            }

            return Misc.OffsetFromEpoch(TimeSpan.FromSeconds(timestamp));
        }

        public DateTime? GetDocumentExpiration(string documentId)
        {
            var timestamp = IsOpen ? Native.c4doc_getExpiration(Forest, documentId) : 0UL;
            if(timestamp == 0UL) {
                return null;
            }

            return Misc.OffsetFromEpoch(TimeSpan.FromSeconds(timestamp));
        }

        public void SetDocumentExpiration(string documentId, DateTime? expiration)
        {
            if(!IsOpen) {
                return;
            }

            if (expiration.HasValue) {
                var timestamp = (ulong)expiration.Value.ToUniversalTime().TimeSinceEpoch().TotalSeconds;
                ForestDBBridge.Check(err => Native.c4doc_setExpiration(Forest, documentId, timestamp, err));
            } else {
                ForestDBBridge.Check(err => Native.c4doc_setExpiration(Forest, documentId, 0UL, err));
            }
        }

        public RevisionInternal GetParentRevision(RevisionInternal rev)
        {
            var retVal = default(RevisionInternal);
            WithC4Document(rev.DocID, rev.RevID, false, false, doc =>
            {
                if(!Native.c4doc_selectParentRevision(doc)) {
                    return;
                }

                ForestDBBridge.Check(err => Native.c4doc_loadRevisionBody(doc, err));
                retVal = new RevisionInternal((string)doc->docID, doc->selectedRev.revID.AsRevID(), doc->selectedRev.IsDeleted);
                retVal.Sequence = (long)doc->selectedRev.sequence;
                retVal.SetBody(new Body(doc->selectedRev.body));
            });

            return retVal;
        }

        public RevisionList GetAllDocumentRevisions(string docId, bool onlyCurrent, bool includeDeleted)
        {
            var retVal = default(RevisionList);
            WithC4Document(docId, null, false, false, doc =>
            {
                using(var enumerator = new CBForestHistoryEnumerator(doc, onlyCurrent, false)) {
                    var expression = includeDeleted ?
                        enumerator.Select (x => new ForestRevisionInternal (x.GetDocument (), false)) :
                        enumerator.Where (x => !x.SelectedRev.IsDeleted).Select (x => new ForestRevisionInternal (x.GetDocument (), false));
                    retVal = new RevisionList(expression.Cast<RevisionInternal>().ToList());
                }
            });

            return retVal;
        }

        public IEnumerable<RevisionID> GetPossibleAncestors(RevisionInternal rev, int limit, ValueTypePtr<bool> haveBodies)
        {
            haveBodies.Value = true;
            var returnedCount = 0;
            var generation = rev.RevID.Generation;
            for(int current = 1; current >= 0; current--) {
                var enumerator = GetHistoryEnumerator(rev, generation, current == 1);
                if(enumerator == null) {
                    yield break;
                }

                foreach(var next in enumerator) {
                    var flags = next.SelectedRev.flags;
                    var tmp = Native.c4rev_getGeneration(next.SelectedRev.revID);
                    if(flags.HasFlag(C4RevisionFlags.RevLeaf) == (current == 1) &&
                        Native.c4rev_getGeneration(next.SelectedRev.revID) < generation) {
                        if(haveBodies && !next.HasRevisionBody) {
                            haveBodies.Value = false;
                        }

                        yield return next.SelectedRev.revID.AsRevID();
                        if(limit > 0 && ++returnedCount >= limit) {
                            break;
                        }
                    }
                }

                if(returnedCount != 0) {
                    yield break;
                }
            }
        }

        public RevisionID FindCommonAncestor(RevisionInternal rev, IEnumerable<RevisionID> revIds)
        {
            var generation = rev.RevID.Generation;
            var revIdArray = revIds == null ? null : revIds.ToList();
            if(generation <= 1 || revIdArray == null || revIdArray.Count == 0) {
                return null;
            }

            revIdArray.Sort();
            var commonAncestor = default(RevisionID);
            WithC4Document(rev.DocID, null, false, false, doc =>
            {
                foreach(var possibleRevId in revIds) {
                    if(possibleRevId.Generation <= generation &&
                        Native.c4doc_selectRevision(doc, possibleRevId.ToString(), false, null)) {
                        commonAncestor = possibleRevId;
                        return;
                    }
                }
            });

            return commonAncestor;
        }

        public IList<RevisionID> GetRevisionHistory(RevisionInternal rev, ICollection<RevisionID> ancestorRevIds)
        {
            var history = new List<RevisionID>();
            WithC4Document(rev.DocID, rev.RevID, false, false, doc =>
            {
                var enumerator = new CBForestHistoryEnumerator(doc, false);
                foreach(var next in enumerator) {
                    var revId = next.SelectedRev.revID.AsRevID();
                    history.Add(revId);
                    if(ancestorRevIds != null && ancestorRevIds.Contains(revId)) {
                        break;
                    }
                }
            });

            return history;
        }

        public RevisionList ChangesSince(long lastSequence, ChangesOptions options, RevisionFilter filter)
        {
            // http://wiki.apache.org/couchdb/HTTP_database_API#Changes
            // Translate options to ForestDB:
            if(options.Descending) {
                // https://github.com/couchbase/couchbase-lite-ios/issues/641
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.NotImplemented, TAG,
                    "Descending ChangesSince is not currently implemented " +
                    "(see https://github.com/couchbase/couchbase-lite-ios/issues/641)");
            }

            var forestOps = C4EnumeratorOptions.DEFAULT;
            forestOps.flags |= C4EnumeratorFlags.IncludeDeleted | C4EnumeratorFlags.IncludeNonConflicted;
            if(options.IncludeDocs || options.IncludeConflicts || filter != null) {
                forestOps.flags |= C4EnumeratorFlags.IncludeBodies;
            }

            var changes = new RevisionList();
            var e = new CBForestDocEnumerator(Forest, lastSequence, forestOps);
            foreach (var next in e) {
                var revs = default(IEnumerable<RevisionInternal>);
                if(options.IncludeConflicts) {
                    using(var enumerator = new CBForestHistoryEnumerator(next.GetDocument(), true, false)) {
                        var includeBody = forestOps.flags.HasFlag(C4EnumeratorFlags.IncludeBodies);
                        revs = enumerator.Select<CBForestDocStatus, RevisionInternal>(x => new ForestRevisionInternal(x.GetDocument(), includeBody)).ToList();
                    }
                } else {
                    revs = new List<RevisionInternal> { new ForestRevisionInternal(next.GetDocument(), forestOps.flags.HasFlag(C4EnumeratorFlags.IncludeBodies)) };
                }

                foreach(var rev in revs) {
                    Debug.Assert(rev != null);
                    if(filter == null || filter(rev)) {
                        if(!options.IncludeDocs) {
                            rev.SetBody(null);
                        }

                        if(filter == null || filter(rev)) {
                            changes.Add(rev);
                        }
                    }
                }
            }

            if(options.SortBySequence) {
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
                if(current++ >= options.Limit) {
                    yield break;
                }

                var sequenceNumber = 0L;
                var docID = next.CurrentDocID;
                remainingIDs.Remove(docID);
                var value = default(IDictionary<string, object>);
                if(next.Exists) {
                    sequenceNumber = (long)next.SelectedRev.sequence;
                    var conflicts = default(IList<string>);
                    if(options.AllDocsMode >= AllDocsMode.ShowConflicts && next.IsConflicted) {
                        SelectCurrentRevision(next);
                        LoadRevisionBody(next);
                        using(var innerEnumerator = GetHistoryFromSequence(next.Sequence)) {
                            conflicts = innerEnumerator.Select(x => (string)x.SelectedRev.revID).ToList();
                        }

                        if(conflicts.Count == 1) {
                            conflicts = null;
                        }
                    }

                    bool valid = conflicts != null || options.AllDocsMode != AllDocsMode.OnlyConflicts;
                    if(!valid) {
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
                if(options.Filter == null || options.Filter(row)) {
                    yield return row;
                } else {
                    Log.To.Query.V(TAG, "   ... on 2nd thought, filter predicate skipped that row");
                }
            }

            foreach(var docId in remainingIDs) {
                var value = GetAllDocsEntry(docId);


                var row = new QueryRow(value != null ? docId as string : null, 0, docId, value, null, null);
                if(options.Filter == null || options.Filter(row)) {
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
                foreach(var rev in sortedRevs) {
                    if(rev.DocID != lastDocId) {
                        lastDocId = rev.DocID;
                        Native.c4doc_free(doc);
                        doc = Native.c4doc_get(Forest, lastDocId, true, null);
                    }

                    if(doc == null) {
                        continue;
                    }

                    rev.RevID.PinAndUse(slice =>
                    {
                        if(Native.c4doc_selectRevision(doc, slice, false, null)) {
                            while (revs.Contains (rev)) {
                                removedCount++;
                                revs.Remove (rev);
                            }
                        }
                    });
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
                if(!docInfo->HasAttachments || (docInfo->IsDeleted && !docInfo->IsConflicted)) {
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
                            var attachments = rev.Get("_attachments").AsDictionary<string, IDictionary<string, object>>();
                            if(attachments == null) {
                                continue;
                            }

                            foreach(var entry in attachments) {
                                try {
                                    var key = new BlobKey(entry.Value.GetCast<string>("digest"));
                                    keys.Add(key);
                                } catch(Exception) {
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
            if(docsToRev.Count == 0) {
                return result;
            }

            Log.To.Database.I(TAG, "Purging {0} docs...", docsToRev.Count);
            RunInTransaction(() =>
            {
                foreach(var docRevPair in docsToRev) {
                    var docID = docRevPair.Key;
                    WithC4Document(docID, null, false, false, doc => {
                        ;
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

        public IList<string> PurgeExpired()
        {
            var results = new List<string>();
            RunInTransaction (() => {
                foreach (var expired in new CBForestExpiryEnumerator (Forest, true)) {
                    results.Add (expired);
                }

                var purgeMap = results.ToDictionary<string, string, IList<string>> (x => x, x => new List<string> { "*" });
                PurgeRevisions (purgeMap);

                return true;
            });
            return results;
        }

        public RevisionInternal GetLocalDocument(string docId, RevisionID revId)
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

                var gotRevId = doc->meta.AsRevID();
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

                properties.SetDocRevID(docId, gotRevId);
                retVal = new RevisionInternal(docId, revId, false);
                retVal.SetProperties(properties);
            });

            return retVal;
        }

        public RevisionInternal PutLocalRevision(RevisionInternal revision, RevisionID prevRevId, bool obeyMVCC)
        {
            var docId = revision.DocID;
            if(!docId.StartsWith("_local/")) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadId, TAG,
                    "Invalid document ID ({0}) in write operation, it must start with _local/",
                    new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure));
            }

            if(revision.Deleted) {
                DeleteLocalRevision(docId, prevRevId, obeyMVCC);
                return revision;
            }

            var result = default(RevisionInternal);
            RunInTransaction(() =>
            {
                var json = Manager.GetObjectMapper().WriteValueAsString(revision.GetProperties(), true);
                WithC4Raw(docId, "_local", doc =>
                {
                    var generation = prevRevId == null ? 0 : prevRevId.Generation;
                    if(obeyMVCC) {
                        var currentRevId = (doc != null ? doc->meta.AsRevID() : null);
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

                    var newRevId = String.Format("{0}-local", ++generation).AsRevID();
                    ForestDBBridge.Check(err => Native.c4raw_put(Forest, "_local", docId, newRevId.ToString(), json, err));
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

        public RevisionInternal PutRevision(string inDocId, RevisionID inPrevRevId, IDictionary<string, object> properties,
            bool deleting, bool allowConflict, Uri source, StoreValidation validationBlock)
        {
            if(_config.HasFlag(C4DatabaseFlags.ReadOnly)) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Forbidden, TAG,
                    "Attempting to write to a readonly database (PutRevision)");
            }

            if(inDocId == null) {
                inDocId = Misc.CreateGUID();
            }

            C4Document* doc = null;
            var putRev = default(RevisionInternal);
            var change = default(DocumentChange);
            var success = RunInTransaction(() =>
            {
                try {
                    
                    var docId = inDocId;
                    var prevRevId = inPrevRevId;

                    // https://github.com/couchbase/couchbase-lite-net/issues/749
                    // Need to ensure revpos is correct for a revision inserted on top
                    // of a deletion
                    var existing = (C4Document*)ForestDBBridge.Check(err => Native.c4doc_getForPut(Forest, docId, prevRevId?.ToString(), deleting,
                        allowConflict, err));

                    if(existing->IsDeleted) {
                        var attachments = properties.CblAttachments();
                        if(attachments != null) {
                            foreach(var attach in attachments) {
                                var metadata = attach.Value.AsDictionary<string, object>();
                                if(metadata != null) {
                                    metadata["revpos"] = existing->revID.AsRevID().Generation + 1;
                                }
                            }
                        }
                    }

                    var json = default(string);
                    if(properties != null) {
                        json = Manager.GetObjectMapper().WriteValueAsString(Database.StripDocumentJSON(properties), true);
                    } else {
                        json = "{}";
                    }

                    C4DocPutRequest rq = new C4DocPutRequest {
                        body = json,
                        docID = docId,
                        deletion = deleting,
                        hasAttachments = properties?.Get("_attachments") != null,
                        existingRevision = false,
                        allowConflict = allowConflict,
                        history = prevRevId == null ? null : new[] { prevRevId.ToString() },
                        save = false
                    };

                    UIntPtr commonAncestorIndex = UIntPtr.Zero;
                    doc = (C4Document*)ForestDBBridge.Check(err =>
                    {
                        UIntPtr tmp;
                        var retVal = Native.c4doc_put(Forest, rq, &tmp, err);
                        commonAncestorIndex = tmp;
                        return retVal;
                    });

                    if(docId == null) {
                        docId = (string)doc->docID;
                    }

                    var newRevID = doc->selectedRev.revID.AsRevID();

                    Body body = null;
                    if(properties != null) {
                        properties.SetDocRevID(docId, newRevID);
                        body = new Body(properties);
                    }

                    putRev = new RevisionInternal(docId, newRevID, deleting, body);
                    if((uint)commonAncestorIndex == 0U) {
                        return true;
                    }

                    if(validationBlock != null) {
                        var prevRev = default(RevisionInternal);
                        if(Native.c4doc_selectParentRevision(doc)) {
                            prevRev = new ForestRevisionInternal(doc, false);
                        }

                        var status = validationBlock(putRev, prevRev, prevRev == null ? null : prevRev.RevID);
                        if(status.IsError) {
                            Log.To.Validation.I(TAG, "{0} ({1}) failed validation", new SecureLogString(docId, LogMessageSensitivity.PotentiallyInsecure), new SecureLogString(newRevID, LogMessageSensitivity.PotentiallyInsecure));
                            throw new CouchbaseLiteException("A document failed validation", status.Code);
                        }
                    }

                    var isWinner = SaveDocument(doc, newRevID, properties);
                    putRev.Sequence = (long)doc->sequence;
                    change = ChangeWithNewRevision(putRev, isWinner, doc, null);
                    return true;
                } finally {
                    Native.c4doc_free(doc);
                }
            });

            if(!success) {
                return null;
            }

            if(Delegate != null && change != null) {
                Delegate.DatabaseStorageChanged(change);
            }

            return putRev;
        }

        public void ForceInsert(RevisionInternal inRev, IList<RevisionID> revHistory, StoreValidation validationBlock, Uri source)
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
                        inRev.GetAttachments() != null, revHistory.Select(x => x.ToString()).ToArray(), err));

                    // Save updated doc back to the database:
                    var isWinner = SaveDocument(doc, revHistory[0], inRev.GetProperties());
                    inRev.Sequence = (long)doc->sequence;
                    Log.To.Database.D(TAG, "Saved {0}", inRev.DocID);
                    change = ChangeWithNewRevision(inRev, isWinner, doc, source);
                });

                return true;
            });

            if(change != null && Delegate != null) {
                Delegate.DatabaseStorageChanged(change);
            }
        }

        public IViewStore GetViewStorage(string name, bool create)
        {
            var view = _views[name];
            if(view == null) {
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
            return System.IO.Directory.GetFiles(Directory, "*." + ForestDBViewStore.VIEW_INDEX_PATH_EXTENSION).
                Select(x => ForestDBViewStore.FileNameToViewName(Path.GetFileName(x)));
        }

        #endregion
    }

    #endregion
}
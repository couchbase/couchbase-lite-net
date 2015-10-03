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
using System.IO;

using CBForest;
using System.Diagnostics;
using Couchbase.Lite.Util;
using Couchbase.Lite.Internal;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Store
{
    internal unsafe delegate void C4DocumentActionDelegate(C4Document* doc);

    internal unsafe delegate bool C4TryLogicDelegate1(C4Error* err);

    internal unsafe delegate void* C4TryLogicDelegate2(C4Error *err);

    internal unsafe delegate bool C4RevisionSelector(C4Document *doc);

    public sealed class CBForestException : ApplicationException
    {
        private readonly C4ErrorCode _code;
        private readonly C4ErrorDomain _domain;

        public C4ErrorCode Code 
        {
            get {
                return _code;
            }
        }

        public C4ErrorDomain Domain
        {
            get {
                return _domain;
            }
        }

        internal CBForestException(C4ErrorCode code, C4ErrorDomain domain) 
            : base(String.Format("CBForest exception ({0} : {1})", domain, code))
        {
            _code = code;
            _domain = domain;
        }
    }

    internal unsafe static class ForestDBBridge {
        public static void Check(C4TryLogicDelegate1 block)
        {
            var err = default(C4Error);
            if (block(&err)) {
                return;
            }

            throw new CouchbaseLiteException(new CBForestException(err.code, err.domain), StatusCode.DbError);
        }

        public static void* Check(C4TryLogicDelegate2 block) where T : struct
        {
            var err = default(C4Error);
            var obj = block(&err);
            if (obj != null) {
                return obj;
            }

            throw new CouchbaseLiteException(new CBForestException(err.code, err.domain), StatusCode.DbError);
        }
    }

    internal unsafe sealed class ForestDBCouchStore : ICouchStore
    {
        private const int DEFAULT_MAX_REV_TREE_DEPTH = 20;
        private const string DB_FILENAME = "db.forest";
        private const string TAG = "ForestDBCouchStore";

        private readonly string _directory;
        private C4DatabaseFlags _config;
        private C4Database *_forest;
        private SymmetricKey _encryptionKey;
        private int _transactionLevel;

        public bool AutoCompact { get; set; }

        public int MaxRevTreeDepth { get; set; }

        public ICouchStoreDelegate Delegate { get; set; }

        public int DocumentCount
        {
            get {
                return Native.c4db_getDocumentCount(_forest);
            }
        }

        public long LastSequence
        {
            get {
                return Native.c4db_getLastSequence(_forest);
            }
        }

        public bool InTransaction
        {
            get {
                return Native.c4db_isInTransaction(_forest);
            }
        }

        public ForestDBCouchStore()
        {
            AutoCompact = true;
            MaxRevTreeDepth = DEFAULT_MAX_REV_TREE_DEPTH;
        }

        public void GetDocument(string docId, long sequence)
        {
            throw NotSupportedException("C API lacks this feature");
        } 

        private void Reopen()
        {
            if (_encryptionKey != null) {
                throw new NotSupportedException("Encryption needs to be implemented in the C API");
            }

            var forestPath = Path.Combine(_directory, DB_FILENAME);
            _forest = (C4Database*)ForestDBBridge.Check(err => Native.c4db_open(forestPath, _config, err));
        }

        private void WithC4Document(string docId, string revId, bool withBody, C4DocumentActionDelegate block)
        {
            var doc = (C4Document*)ForestDBBridge.Check(err => Native.c4doc_get(_forest, docId, true, err));
            ForestDBBridge.Check(err => Native.c4doc_selectRevision(doc, revId, withBody, err));

            block(doc);

            Native.c4doc_free(doc);
        }

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
            throw new NotSupportedException("C API needs to support encryption");
        }

        public void Compact()
        {
            throw new NotSupportedException("C API needs a compact function");
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
                retVal = new RevisionInternal(docId, revId, doc->selectedRev.flags.HasFlag(C4RevisionFlags.RevDeleted));
                retVal.SetSequence((long)doc->selectedRev.sequence);
                retVal.SetBody(new Body(doc->selectedRev.body));
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
            var retVal = new RevisionList();
            WithC4Document(docId, null, false, doc =>
            {
                C4RevisionSelector continuationLogic = onlyCurrent ? 
                    (d => Native.c4doc_selectNextLeafRevision(d, true, false, null)) : Native.c4doc_selectNextRevision;

                do {
                    retVal.Add(new RevisionInternal((string)doc->docID, (string)doc->selectedRev.revID, 
                        doc->selectedRev.flags.HasFlag(C4RevisionFlags.RevDeleted)));
                } while(continuationLogic(doc));
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

            throw new NotSupportedException("Need additional C API elements");
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
                var newRev = new RevisionInternal(docId, (string)doc->selectedRev.revID, 
                    doc->selectedRev.flags.HasFlag(C4RevisionFlags.RevDeleted));
                while(!ancestorRevIds.Contains(newRev.GetRevId())) {
                    history.Add(newRev);
                    if(!Native.c4doc_selectParentRevision(doc)) {
                        return;
                    }

                    newRev = new RevisionInternal(docId, (string)doc->selectedRev.revID, 
                        doc->selectedRev.flags.HasFlag(C4RevisionFlags.RevDeleted));
                } 
            });

            throw new NotSupportedException("C API lacks isBodyAvailable");
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
            var e = (C4DocEnumerator*)ForestDBBridge.Check(err => Native.c4db_enumerateChanges(_forest, (ulong)lastSequence, &forestOps, err));
            var doc = default(C4Document*);
            while ((doc = Native.c4enum_nextDocument(e, null)) != null) {
                var revIDs = default(IList<string>);
                if (options.IsIncludeConflicts()) {

                } else {
                    revIDs = new List<string> { (string)doc->revID };
                }
            }
        }
    }
}


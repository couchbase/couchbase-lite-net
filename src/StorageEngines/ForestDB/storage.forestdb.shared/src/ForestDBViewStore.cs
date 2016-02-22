//
// ForestDBViewStore.cs
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
#define PARSED_KEYS
//#define CONNECTION_PER_THREAD

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using CBForest;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using Couchbase.Lite.Views;
using Couchbase.Lite.Storage.ForestDB.Internal;
using Couchbase.Lite.Store;


namespace Couchbase.Lite.Storage.ForestDB
{
    internal unsafe delegate void C4KeyActionDelegate(C4Key*[] key);

    internal sealed unsafe class ForestDBViewStore : IViewStore, IQueryRowStore
    {
        private const string TAG = "ForestDBViewStore";
        internal const string VIEW_INDEX_PATH_EXTENSION = "viewindex";

        private ForestDBCouchStore _dbStorage;
        private string _path;
#if CONNECTION_PER_THREAD
        private ConcurrentDictionary<int, IntPtr> _fdbConnections =
            new ConcurrentDictionary<int, IntPtr>();
#endif

        public IViewStoreDelegate Delegate { get; set; }

        public string Name { get; private set; }

        private C4View* IndexDB
        {
            get {
    #if CONNECTION_PER_THREAD
                return (C4View*)_fdbConnections.GetOrAdd(Thread.CurrentThread.ManagedThreadId, 
                    x => new IntPtr(OpenIndex())).ToPointer();
    #else
                if(_indexDB == null) {
                    _indexDB = OpenIndex();
                }

                return _indexDB;
    #endif
                
            }
        }

#if !CONNECTION_PER_THREAD
        private C4View *_indexDB;
#endif


        public int TotalRows 
        {
            get {
                try {
                    return (int)Native.c4view_getTotalRows(IndexDB);
                } catch(Exception e) {
                    Log.W(TAG, "Exception opening index while getting total rows", e);
                    return 0;
                }
            }
        }

        public long LastSequenceChangedAt
        {
            get {
                try {
                    return (long)Native.c4view_getLastSequenceChangedAt(IndexDB);
                } catch(Exception e) {
                    Log.W(TAG, "Exception opening index while getting last sequence changed at", e);
                    return 0;
                }
            }
        }

        public long LastSequenceIndexed
        {
            get {
                try {
                    return (long)Native.c4view_getLastSequenceIndexed(IndexDB);
                } catch(Exception e) {
                    Log.W(TAG, "Exception opening index while getting last sequence indexed", e);
                    return 0;
                }
            }
        }

        public ForestDBViewStore(ForestDBCouchStore dbStorage, string name, bool create)
        {
            Debug.Assert(dbStorage != null);
            Debug.Assert(name != null);
            _dbStorage = dbStorage;
            Name = name;

            var filename = ViewNameToFilename(name);
            _path = Path.Combine(_dbStorage.Directory, filename);
            var files = System.IO.Directory.GetFiles(_dbStorage.Directory, filename + "*");
            if (files.Length == 0) {
                if (!create) {
                    throw new InvalidOperationException(String.Format(
                        "Create is false but no db file exists at {0}", _path));
                }

                OpenIndexWithOptions(C4DatabaseFlags.Create, true);
            }
        }

        public static void WithC4Keys(object[] keySources, bool writeNull, C4KeyActionDelegate action)
        {
            if (keySources == null) {
                action(null);
                return;
            }

            var c4Keys = new C4Key*[keySources.Length];
            for (int i = 0; i < keySources.Length; i++) {
                if (keySources[i] == null && !writeNull) {
                    c4Keys[i] = null;
                } else {
                    c4Keys[i] = CouchbaseBridge.SerializeToKey(keySources[i]);
                }
            }

            try {
                action(c4Keys);
            } finally {
                foreach (C4Key *key in c4Keys) {
                    Native.c4key_free(key);
                }
            }
        }

        public AtomicAction ActionToChangeEncryptionKey(SymmetricKey newKey)
        {
            return new AtomicAction(() => {
                ForestDBBridge.Check(err => 
                {
                    var newc4key = default(C4EncryptionKey);
                    if (newKey != null) {
                        newc4key = new C4EncryptionKey(newKey.KeyData);
                    }

                    return Native.c4view_rekey(IndexDB, &newc4key, err);
                });

                CloseIndex();
           }, null, null);
        }

        internal static string FileNameToViewName(string filename)
        {
            if(!filename.Contains(VIEW_INDEX_PATH_EXTENSION)) {
                return null;
            }

            var parts = filename.Split('.');
            return UnescapeString(parts[0]);
        }

        private void CloseIndex()
        {
#if CONNECTION_PER_THREAD
            var connections = _fdbConnections.Values.ToArray();
            _fdbConnections.Clear();
            foreach (var connection in connections) {
                ForestDBBridge.Check(err => Native.c4view_close((C4View*)connection.ToPointer(), err));
            }
#else
            var indexDb = _indexDB;
            _indexDB = null;
            ForestDBBridge.Check(err => Native.c4view_close(indexDb, err));
#endif
        }

        private C4View* OpenIndexWithOptions(C4DatabaseFlags options, bool dryRun = false)
        {
                var retVal = (C4View*)ForestDBBridge.Check(err =>
                {
                    var encryptionKey = default(C4EncryptionKey);
                    if(_dbStorage.EncryptionKey != null) {
                        encryptionKey = new C4EncryptionKey(_dbStorage.EncryptionKey.KeyData);
                    }

                    return Native.c4view_open(_dbStorage.Forest, _path, Name, dryRun  ? "0" : Delegate.MapVersion, options, 
                        &encryptionKey, err);
                });

                if (dryRun) {
                    ForestDBBridge.Check(err => Native.c4view_close(retVal, err));
                }

            return retVal;
        }

        private C4View* OpenIndex()
        {
            return OpenIndexWithOptions((C4DatabaseFlags)0);
        }

        internal static string ViewNameToFilename(string viewName)
        {
            return Path.ChangeExtension(EscapeString(viewName), VIEW_INDEX_PATH_EXTENSION);
        }

        private static bool IsLegalChar(byte c)
        {
            // POSIX legal characters
            return (c > 47 && c < 58) ||
                (c > 64 && c < 91) ||
                (c > 96 && c < 123) ||
                c == 45 || c == 46 || c == 95 || c > 127;
        }

        private unsafe static string EscapeString(string unescaped)
        {
            var sb = new StringBuilder();
            var length = 0;
            var buffer = new byte[6];
            fixed(byte* bufPtr = buffer)
            fixed(char* ptr = unescaped) {
                var currentCharPtr = ptr;
                while(length < unescaped.Length) {
                    var numBytes = Encoding.UTF8.GetBytes(currentCharPtr, 1, bufPtr, 6);
                    if(IsLegalChar(buffer[0])) {
                        sb.Append(*currentCharPtr);
                    } else {
                        sb.AppendFormat("@{0}", Misc.ConvertToHex(buffer, numBytes));
                    }

                    currentCharPtr++;
                    length++;
                }
            }

            return sb.ToString();
        }

        private static string UnescapeString(string escaped)
        {
            var sb = new StringBuilder();
            var buffer = new char[2];
            var charCounter = 0;
            foreach(var c in escaped) {
                if(c == '@') {
                    charCounter = 1;
                    continue;
                } else if(charCounter > 0) {
                    buffer[charCounter - 1] = c;
                    if(charCounter == 2) {
                        sb.Append((char)Convert.ToByte(new string(buffer), 16));
                        charCounter = 0;
                    } else {
                        charCounter++;
                    }
                } else {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static string ViewNames(IEnumerable<IViewStore> inputViews)
        {
            var names = inputViews.Select(x => x.Name);
            return String.Join(", ", names.ToStringArray());
        }

        private CBForestQueryEnumerator QueryEnumeratorWithOptions(QueryOptions options)
        {
            var enumerator = default(C4QueryEnumerator*);
            using(var startkeydocid_ = new C4String(options.StartKeyDocId))
            using(var endkeydocid_ = new C4String(options.EndKeyDocId)) {
                WithC4Keys(new object[] { options.StartKey, options.EndKey }, false, startEndKey =>
                    WithC4Keys(options.Keys == null ? null : options.Keys.ToArray(), true, c4keys =>
                    {
                        var opts = C4QueryOptions.DEFAULT;
                        opts.descending = options.Descending;
                        opts.endKey = startEndKey[1];
                        opts.endKeyDocID = endkeydocid_.AsC4Slice();
                        opts.inclusiveEnd = options.InclusiveEnd;
                        opts.inclusiveStart = options.InclusiveStart;
                        if(c4keys != null) {
                            opts.keysCount = (uint)c4keys.Length;
                        }

                        opts.limit = (ulong)options.Limit;
                        opts.skip = (ulong)options.Skip;
                        opts.startKey = startEndKey[0];
                        opts.startKeyDocID = startkeydocid_.AsC4Slice();
                        fixed (C4Key** keysPtr = c4keys) {
                            opts.keys = keysPtr;
                            enumerator = (C4QueryEnumerator*)ForestDBBridge.Check(err => {
                                var localOpts = opts;
                                return Native.c4view_query(IndexDB, &localOpts, err);
                            });
                        }
                    })
                );
            }

            return new CBForestQueryEnumerator(enumerator);
        }

        #if PARSED_KEYS

        private static bool GroupTogether(object lastKey, object key, int groupLevel)
        {
            if (groupLevel == 0) {
                return !((lastKey == null) || (key == null)) && lastKey.Equals(key);
            }

            var lastArr = lastKey as IList;
            var arr = key as IList;
            if (lastArr == null || arr == null) {
                return groupLevel == 1 && (!((lastKey == null) || (key == null)) && lastKey.Equals(key));
            }

            var level = Math.Min(groupLevel, Math.Min(lastArr.Count, arr.Count));
            for (int i = 0; i < level; i++) {
                if (!lastArr[i].Equals(arr[i])) {
                    return false;
                }
            }

            return true;
        }

        private static object GroupKey(object key, int groupLevel)
        {
            var arr = key.AsList<object>();
            if (groupLevel > 0 && arr != null && arr.Count > groupLevel) {
                return new Couchbase.Lite.Util.ArraySegment<object>(arr.ToArray(), 0, groupLevel);
            }

            return key;
        }

        #endif

        private static object CallReduce(ReduceDelegate reduce, IList<object> keys, IList<object> vals)
        {
            if (reduce == null) {
                return null;
            }

            #if PARSED_KEYS
            var lazyKeys = keys;
            #else
            var lazyKeys = new LazyJsonArray(keys);
            #endif

            var lazyValues = new LazyJsonArray(vals);
            try {
                var result = reduce(lazyKeys, lazyValues, false);
                if(result != null) {
                    return result;
                }
            } catch(Exception e) {
                Log.W(TAG, "Exception in reduce block", e);
            }

            return null;
        }

        private QueryRow CreateReducedRow(object key, bool group, int groupLevel, ReduceDelegate reduce, Func<QueryRow, bool> filter,
            IList<object> keysToReduce, IList<object> valsToReduce)
        {
            try {
                var row = new QueryRow(null, 0, group ? GroupKey(key, groupLevel) : null, 
                    CallReduce(reduce, keysToReduce, valsToReduce), null, this);
                if (filter != null && filter(row)) {
                    row = null;
                }

                return row;
            } catch(CouchbaseLiteException) {
                Log.W(TAG, "Failed to run reduce query for {0}", Name);
                throw;
            } catch(Exception e) {
                throw new CouchbaseLiteException(String.Format("Error running reduce query for {0}",
                    Name), e) { Code = StatusCode.Exception };
            }
        }

        public void Close()
        {
            CloseIndex();
            _dbStorage.ForgetViewStorage(Name);
        }

        public void DeleteIndex()
        {
            ForestDBBridge.Check(err => Native.c4view_eraseIndex(IndexDB, err));
        }

        public void DeleteView()
        {
            _dbStorage.ForgetViewStorage(Name);
            ForestDBBridge.Check(err => Native.c4view_delete(IndexDB, err));
        }

        public bool SetVersion(string version)
        {
            return true;
        }

        public bool UpdateIndexes(IEnumerable<IViewStore> views)
        {
            Log.D(TAG, "Checking indexes of ({0}) for {1}", ViewNames(views), Name);

            // Creates an array of tuples -> [[view1, view1 last sequence, view1 native handle], 
            // [view2, view2 last sequence, view2 native handle], ...]
            var viewsArray = views.Cast<ForestDBViewStore>().ToArray();
            var viewInfo = viewsArray.Select(x => Tuple.Create(x, x.LastSequenceIndexed)).ToArray();
            var nativeViews = new C4View*[viewsArray.Length];
            for (int i = 0; i < viewsArray.Length; i++) {
                nativeViews[i] = viewsArray[i].IndexDB;
            }

            var indexer = (C4Indexer*)ForestDBBridge.Check(err => Native.c4indexer_begin(_dbStorage.Forest, nativeViews, err));
          
            var enumerator = new CBForestDocEnumerator(indexer);

            var commit = false;
            try {
                foreach(var next in enumerator) {
                    var seq = next.Sequence;

                    for (int i = 0; i < viewInfo.Length; i++) {
                        
                        var info = viewInfo[i];
                        if (seq <= info.Item2) {
                            continue; // This view has already indexed this sequence
                        }

                        var viewDelegate = info.Item1.Delegate;
                        if (viewDelegate == null || viewDelegate.Map == null) {
                            Log.V(TAG, "    {0} has no map block; skipping it", info.Item1.Name);
                            continue;
                        }

                        var rev = new ForestRevisionInternal(next, true);
                        var keys = new List<object>();
                        var values = new List<string>();

                        var conflicts = default(List<string>);
                        foreach(var leaf in new CBForestHistoryEnumerator(_dbStorage.Forest, next.Sequence, true)) {
                            if(leaf.SelectedRev.revID.Equals(leaf.CurrentRevID)) {
                                continue;
                            }

                            if(leaf.IsDeleted) {
                                break;
                            }

                            if(conflicts == null) {
                                conflicts = new List<string>();
                            }

                            conflicts.Add((string)leaf.SelectedRev.revID);
                        }

                        if(conflicts != null) {
                            rev.SetPropertyForKey("_conflicts", conflicts);
                        }

                        try {
                            var props = rev.GetProperties();
                            viewDelegate.Map(props, (key, value) =>
                            {
                                keys.Add(key);
                                if(props == value) {
                                    values.Add("*");
                                } else {
                                    values.Add(Manager.GetObjectMapper().WriteValueAsString(value));
                                }
                            });
                        } catch (Exception e) {
                            Log.W(TAG, String.Format("Exception thrown in map function of {0}", info.Item1.Name), e);
                            continue;
                        }

                        WithC4Keys(keys.ToArray(), true, c4keys =>
                            ForestDBBridge.Check(err => Native.c4indexer_emit(indexer, next.GetDocument(), (uint)i, c4keys, values.ToArray(), err))
                        );
                    }
                }

                commit = true;
            } catch(Exception e) {
                Log.W(TAG, "Error updates indexes", e);
            } finally {
                ForestDBBridge.Check(err => Native.c4indexer_end(indexer, commit, err));
            }

            return true;
        }

        public UpdateJob CreateUpdateJob(IEnumerable<IViewStore> viewsToUpdate)
        {
            var cast = viewsToUpdate.Cast<ForestDBViewStore>();
            return new UpdateJob(UpdateIndexes, viewsToUpdate, from store in cast
                select store._dbStorage.LastSequence);
        }

        public IEnumerable<QueryRow> RegularQuery(QueryOptions options)
        {
            var enumerator = QueryEnumeratorWithOptions(options); 
            foreach (var next in enumerator) {
                
                var key = CouchbaseBridge.DeserializeKey<object>(next.Key);
                var value = (next.Value as IEnumerable<byte>).ToArray();
                var docRevision = default(RevisionInternal); 
                if (value.Length == 1 && value[0] == 42) {
                    docRevision = _dbStorage.GetDocument(next.DocID, null, true);
                } else {
                    docRevision = _dbStorage.GetDocument(next.DocID, null, options.IncludeDocs);
                }

                yield return new QueryRow(next.DocID, next.DocSequence, key, value, docRevision, this);
            }
        }

        public IEnumerable<QueryRow> ReducedQuery(QueryOptions options)
        {
            var groupLevel = options.GroupLevel;
            var group = options.Group || groupLevel > 0;

            var reduce = Delegate == null ? null : Delegate.Reduce;
            if (options.ReduceSpecified) {
                if (!options.Reduce) {
                    reduce = null;
                } else if (reduce == null) {
                    throw new CouchbaseLiteException(String.Format(
                        "Cannot use reduce option in view {0} which has no reduce block defined", Name), 
                        StatusCode.BadParam);

                }
            }

            var lastKey = default(object);
            var filter = options.Filter;
            var keysToReduce = default(IList<object>);
            var valsToReduce = default(IList<object>);
            if (reduce != null) {
                keysToReduce = new List<object>(100);
                valsToReduce = new List<object>(100);
            }

            var enumerator = QueryEnumeratorWithOptions(options);

            var row = default(QueryRow);
            foreach (var next in enumerator) {
                var key = CouchbaseBridge.DeserializeKey<object>(next.Key);
                var value = default(object);
                if (lastKey != null && (key == null || (group && !GroupTogether(lastKey, key, groupLevel)))) {
                    // key doesn't match lastKey; emit a grouped/reduced row for what came before:
                    row = CreateReducedRow(lastKey, group, groupLevel, reduce, filter, keysToReduce, valsToReduce);
                    keysToReduce.Clear();
                    valsToReduce.Clear();
                    if(row != null) {
                        var rowCopy = row;
                        row = null;
                        yield return rowCopy;
                    }
                }

                if (key != null && reduce != null) {
                    // Add this key/value to the list to be reduced:
                    keysToReduce.Add(key);
                    var nextVal = next.Value;
                    if (nextVal.size == 1 && nextVal.ElementAt(0) == (byte)'*') {
                        try {
                            var rev = _dbStorage.GetDocument(next.DocID, next.DocSequence);
                            value = rev.GetProperties();
                        } catch(CouchbaseLiteException e) {
                            Log.W(TAG, "Couldn't load doc for row value: status {0}", e.CBLStatus.Code);
                        } catch(Exception e) {
                            Log.W(TAG, "Couldn't load doc for row value", e);
                        }
                    } else {
                        value = Manager.GetObjectMapper().ReadValue<object>(next.Value);
                    }

                    valsToReduce.Add(value);
                }

                lastKey = key;
            }

            row = CreateReducedRow(lastKey, group, groupLevel, reduce, filter, keysToReduce, valsToReduce);
            if(row != null) {
                yield return row;
            }
        }

        public IQueryRowStore StorageForQueryRow(QueryRow row)
        {
            return this;
        }

        public IEnumerable<IDictionary<string, object>> Dump()
        {
            var enumerator = QueryEnumeratorWithOptions(new QueryOptions());
            foreach (var next in enumerator) {
                yield return new Dictionary<string, object> {
                    { "seq", next.DocSequence },
                    { "key", next.KeyJSON },
                    { "val", next.ValueJSON }
                };
            }
        }

        public bool RowValueIsEntireDoc(object valueData)
        {
            var valueString = valueData as IEnumerable<byte>;
            if (valueString == null) {
                return false;
            }

            bool first = true;
            foreach (var character in valueString) {
                if (!first) {
                    return false;
                }

                if (character != (byte)'*') {
                    return false;
                }

                first = false;
            }

            return true;
        }

        public T ParseRowValue<T>(IEnumerable<byte> valueData)
        {
            return Manager.GetObjectMapper().ReadValue<T>(valueData);
        }

        public IDictionary<string, object> DocumentProperties(string docId, long sequenceNumber)
        {
            return _dbStorage.GetDocument(docId, sequenceNumber).GetProperties();
        }
    }
}

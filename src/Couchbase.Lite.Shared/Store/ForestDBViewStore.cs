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
#if FORESTDB

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using CBForest;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using Couchbase.Lite.Views;

namespace Couchbase.Lite.Store
{
    internal unsafe delegate void C4KeyActionDelegate(C4Key*[] key);

    internal sealed unsafe class ForestDBViewStore : IViewStore, IQueryRowStore
    {
        private const string TAG = "ForestDBViewStore";
        internal const string VIEW_INDEX_PATH_EXTENSION = "viewindex";

        private ForestDBCouchStore _dbStorage;
        private string _path;
        private C4View* _indexDB;

        public IViewStoreDelegate Delegate { get; set; }

        public string Name { get; private set; }

        public int TotalRows 
        {
            get {
                try {
                    OpenIndex();
                } catch(Exception e) {
                    Log.W(TAG, "Exception opening index while getting total rows", e);
                    return 0;
                }

                return (int)Native.c4view_getTotalRows(_indexDB);
            }
        }

        public long LastSequenceChangedAt
        {
            get {
                try {
                    OpenIndex();
                } catch(Exception e) {
                    Log.W(TAG, "Exception opening index while getting last sequence changed at", e);
                    return 0;
                }

                return (long)Native.c4view_getLastSequenceChangedAt(_indexDB);
            }
        }

        public long LastSequenceIndexed
        {
            get {
                try {
                    OpenIndex();
                } catch(Exception e) {
                    Log.W(TAG, "Exception opening index while getting last sequence indexed", e);
                    return 0;
                }

                return (long)Native.c4view_getLastSequenceIndexed(_indexDB);
            }
        }

        public ForestDBViewStore(ForestDBCouchStore dbStorage, string name, bool create)
        {
            Debug.Assert(dbStorage != null);
            Debug.Assert(name != null);
            _dbStorage = dbStorage;
            Name = name;

            _path = Path.Combine(_dbStorage.Directory, ViewNameToFilename(name));
            if (!File.Exists(_path)) {
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
                    c4Keys[i] = Manager.GetObjectMapper().SerializeToKey(keySources[i]);
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
            return new AtomicAction(() =>
                ForestDBBridge.Check(err => 
                {
                    var newc4key = default(C4EncryptionKey);
                    if (newKey != null) {
                        newc4key = new C4EncryptionKey(newKey.KeyData);
                    }

                    return Native.c4view_rekey(_indexDB, &newc4key, err);
                }), null, null);
        }

        internal static string FileNameToViewName(string filename)
        {
            if(!filename.EndsWith(VIEW_INDEX_PATH_EXTENSION)) {
                return null;
            }

            if (filename.StartsWith(".")) {
                return null;
            }

            var viewName = Path.ChangeExtension(filename, String.Empty);
            return viewName.Replace(":", "/").TrimEnd('.');
        }

        private void CloseIndex()
        {
            var indexDB = _indexDB;
            _indexDB = null;
            if (indexDB != null) {
                Log.D(TAG, "Closing index");
                ForestDBBridge.Check(err => Native.c4view_close(indexDB, err));
            }
        }

        private C4View* OpenIndexWithOptions(C4DatabaseFlags options, bool dryRun = false)
        {
            if (_indexDB == null) {
                _indexDB = (C4View*)ForestDBBridge.Check(err =>
                {
                    var encryptionKey = default(C4EncryptionKey);
                    if(_dbStorage.EncryptionKey != null) {
                        encryptionKey = new C4EncryptionKey(_dbStorage.EncryptionKey.KeyData);
                    }

                    return Native.c4view_open(_dbStorage.Forest, _path, Name, dryRun  ? "0" : Delegate.MapVersion, options, 
                        &encryptionKey, err);
                });

                if (dryRun) {
                    ForestDBBridge.Check(err => Native.c4view_close(_indexDB, err));
                    _indexDB = null;
                }
            }

            return _indexDB;
        }

        // Needed to call from inside an iterator
        private void OpenIndexNoReturn()
        {
            OpenIndex(); 
        }

        private C4View* OpenIndex()
        {
            if (_indexDB != null) {
                return _indexDB;
            }

            return OpenIndexWithOptions((C4DatabaseFlags)0);
        }

        private static string ViewNameToFilename(string viewName)
        {
            if (viewName.StartsWith(".") || viewName.Contains(":")) {
                return null;
            }

            return Path.ChangeExtension(viewName.Replace('/', ':'), VIEW_INDEX_PATH_EXTENSION);
        }

        private static string ViewNames(IEnumerable<IViewStore> inputViews)
        {
            var names = inputViews.Select(x => x.Name);
            return String.Join(", ", names.ToStringArray());
        }

        private CBForestQueryEnumerator QueryEnumeratorWithOptions(QueryOptions options)
        {
            Debug.Assert(_indexDB != null);
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
                                return Native.c4view_query(_indexDB, &localOpts, err);
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
            ForestDBBridge.Check(err => Native.c4view_eraseIndex(_indexDB, err));
        }

        public void DeleteView()
        {
            _dbStorage.ForgetViewStorage(Name);
            ForestDBBridge.Check(err => Native.c4view_delete(_indexDB, err));
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
                nativeViews[i] = viewsArray[i]._indexDB;
            }

            var indexer = (C4Indexer*)ForestDBBridge.Check(err => Native.c4indexer_begin(_dbStorage.Forest, nativeViews, err));
          
            var enumerator = new CBForestDocEnumerator(indexer);

            var commit = false;
            try {
                foreach(var next in enumerator) {
                    var seq = next.SelectedRev.sequence;

                    for (int i = 0; i < viewInfo.Length; i++) {
                        var info = viewInfo[i];
                        if (seq <= (ulong)info.Item2) {
                            continue; // This view has already indexed this sequence
                        }

                        var viewDelegate = info.Item1.Delegate;
                        if (viewDelegate == null || viewDelegate.Map == null) {
                            Log.V(TAG, "    {0} has no map block; skipping it", info.Item1.Name);
                            continue;
                        }

                        var rev = new RevisionInternal(next, true);
                        var keys = new List<object>();
                        var values = new List<string>();

                        var conflicts = default(List<string>);
                        foreach(var leaf in new CBForestHistoryEnumerator(next, true, false).Skip(1)) {
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
                            viewDelegate.Map(rev.GetProperties(), (key, value) =>
                            {
                                keys.Add(key);
                                values.Add(Manager.GetObjectMapper().WriteValueAsString(value));
                            });
                        } catch (Exception e) {
                            Log.W(TAG, String.Format("Exception thrown in map function of {0}", info.Item1.Name), e);
                            continue;
                        }

                        WithC4Keys(keys.ToArray(), true, c4keys =>
                            ForestDBBridge.Check(err => Native.c4indexer_emit(indexer, next.Document, (uint)i, c4keys, values.ToArray(), err))
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
            OpenIndexNoReturn();
            var enumerator = QueryEnumeratorWithOptions(options); 
            foreach (var next in enumerator) {
                var docRevision = _dbStorage.GetDocument(next.DocID, null, options.IncludeDocs);
                var key = Manager.GetObjectMapper().DeserializeKey<object>(next.Key);
                var value = Manager.GetObjectMapper().ReadValue<object>(next.Value);
                yield return new QueryRow(docRevision.GetDocId(), docRevision.GetSequence(), key, value, docRevision, this);
            }
        }

        public IEnumerable<QueryRow> ReducedQuery(QueryOptions options)
        {
            OpenIndexNoReturn();
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
                var key = Manager.GetObjectMapper().DeserializeKey<object>(next.Key);
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
            OpenIndexNoReturn();
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
            var c4key = Native.c4key_withBytes(valueData);
            var retVal = Manager.GetObjectMapper().DeserializeKey<T>(Native.c4key_read(c4key));
            Native.c4key_free(c4key);
            return retVal;
        }

        public IDictionary<string, object> DocumentProperties(string docId, long sequenceNumber)
        {
            return _dbStorage.GetDocument(docId, sequenceNumber).GetProperties();
        }
    }
}
#endif

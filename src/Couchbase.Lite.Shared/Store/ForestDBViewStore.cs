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
        private const string VIEW_INDEX_PATH_EXTENSION = "viewindex";

        private ForestDBCouchStore _dbStorage;
        private string _path;
        private string _latestMapVersion;
        private C4View* _indexDB;

        public IViewStoreDelegate Delegate { get; set; }

        public string Name { get; private set; }

        public int TotalRows 
        {
            get {
                try {
                    OpenIndex();
                } catch(Exception e) {
                    Log.W(TAG, "Exception opening index while getting total rows");
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
                    Log.W(TAG, "Exception opening index while getting last sequence changed at");
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
                    Log.W(TAG, "Exception opening index while getting last sequence indexed");
                    return 0;
                }

                return (long)Native.c4view_getLastSequenceIndexed(_indexDB);
            }
        }

        public ForestDBViewStore(ForestDBCouchStore dbStorage, string name, bool create, IViewStoreDelegate delegateObject)
        {
            Debug.Assert(dbStorage != null);
            Debug.Assert(name != null);
            Debug.Assert(delegateObject != null);
            Delegate = delegateObject;
            _dbStorage = dbStorage;
            Name = name;

            _path = Path.Combine(_dbStorage.Directory, ViewNameToFilename(name));
            if (!File.Exists(_path)) {
                if (!create) {
                    throw new InvalidOperationException(String.Format(
                        "Create is false but no db file exists at {0}", _path));
                }

                var view = OpenIndexWithOptions(C4DatabaseFlags.Create);
                ForestDBBridge.Check(err => Native.c4view_close(view, err));
            }
        }

        public static void WithC4Keys(object[] keySources, C4KeyActionDelegate action)
        {
            if (keySources == null) {
                action(null);
                return;
            }

            var c4Keys = new C4Key*[keySources.Length];
            for (int i = 0; i < keySources.Length; i++) {
                c4Keys[i] = Manager.GetObjectMapper().SerializeToKey(keySources[i]);
            }

            try {
                action(c4Keys);
            } finally {
                foreach (C4Key *key in c4Keys) {
                    Native.c4key_free(key);
                }
            }
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

        private C4View* OpenIndexWithOptions(C4DatabaseFlags options)
        {
            if (_indexDB == null) {
                _indexDB = (C4View*)ForestDBBridge.Check(err =>
                {
                    var encryptionKey = new C4EncryptionKey();
                    encryptionKey.algorithm = (C4EncryptionType)(-1);
                    encryptionKey.bytes = _dbStorage.EncryptionKey.KeyData;
                    return Native.c4view_open(_dbStorage.Forest, _path, Name, Delegate.MapVersion, options, 
                        &encryptionKey, err);
                });
            }

            return _indexDB;
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

        private C4QueryEnumerator* QueryEnumeratorWithOptions(QueryOptions options)
        {
            Debug.Assert(_indexDB != null);
            var enumerator = default(C4QueryEnumerator*);
            using(var startkeydocid_ = new C4String(options.StartKeyDocId))
            using(var endkeydocid_ = new C4String(options.EndKeyDocId)) {
                WithC4Keys(new object[] { options.StartKey, options.EndKey }, startEndKey =>
                    WithC4Keys(options.Keys == null ? null : options.Keys.ToArray(), c4keys =>
                    {
                        var opts = C4QueryOptions.DEFAULT;
                        opts.descending = options.Descending;
                        opts.endKey = startEndKey[1];
                        opts.endKeyDocID = endkeydocid_.AsC4Slice();
                        opts.inclusiveEnd = options.InclusiveEnd;
                        opts.inclusiveStart = options.InclusiveStart;
                        if(c4keys != null) {
                            opts.keysCount = new UIntPtr((uint)c4keys.Length);
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

            return enumerator;
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

        public void Close()
        {
            //CloseIndex();
            //_dbStorage.ForgetViewStorageNamed(Name);
        }

        public void DeleteIndex()
        {
            ForestDBBridge.Check(err => Native.c4view_eraseIndex(_indexDB, err));
        }

        public void DeleteView()
        {
            ForestDBBridge.Check(err => Native.c4view_delete(_indexDB, err));
        }

        public bool SetVersion(string version)
        {
            return true;
        }

        public bool UpdateIndexes(IEnumerable<IViewStore> views)
        {
            Log.D(TAG, "Checking indexes of ({0}) for {1}", ViewNames(views), Name);
            var nativeViews = views.Cast<ForestDBViewStore>().Select(x => x._indexDB).ToArray();
            var indexer = (C4Indexer*)ForestDBBridge.Check(err => Native.c4indexer_begin(_dbStorage.Forest, nativeViews, 
                nativeViews.Length, err));
            var enumerator = (C4DocEnumerator*)ForestDBBridge.Check(err => Native.c4indexer_enumerateDocuments(indexer, err));
            var doc = default(C4Document*);
            while ((doc = Native.c4enum_nextDocument(enumerator, null)) != null) {
                Native.c4index
            }
        }

        public UpdateJob CreateUpdateJob(IEnumerable<IViewStore> viewsToUpdate)
        {
            var cast = viewsToUpdate.Cast<ForestDBViewStore>();
            return new UpdateJob(UpdateIndexes, viewsToUpdate, from store in cast
                select store._dbStorage.LastSequence);
        }

        public IEnumerable<QueryRow> RegularQuery(QueryOptions options)
        {
            OpenIndex();
            var enumerator = QueryEnumeratorWithOptions(options); 
            while (Native.c4queryenum_next(enumerator, null)) {
                var docRevision = _dbStorage.GetDocument((string)enumerator->docID, null, true);
                var key = Manager.GetObjectMapper().DeserializeKey<object>(enumerator->key);
                var value = Manager.GetObjectMapper().DeserializeKey<object>(enumerator->value);
                yield return new QueryRow(docRevision.GetDocId(), docRevision.GetSequence(), key, value, docRevision, this);
            }

            Native.c4queryenum_free(enumerator);
            yield break;
        }

        public IEnumerable<QueryRow> ReducedQuery(QueryOptions options)
        {
            OpenIndex();
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
            try {
                while (Native.c4queryenum_next(enumerator, null)) {
                    var key = Manager.GetObjectMapper().DeserializeKey<object>(enumerator->key);
                    var value = default(object);
                    if (lastKey != null && (key == null || (group && !GroupTogether(lastKey, key, groupLevel)))) {
                        // key doesn't match lastKey; emit a grouped/reduced row for what came before:
                        row = new QueryRow(null, 0, GroupKey(lastKey, groupLevel), 
                                      CallReduce(reduce, keysToReduce, valsToReduce), null, this);
                        if (filter != null && filter(row)) {
                            row = null;
                        }

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
                        if (Native.c4key_peek(&enumerator->value) == C4KeyToken.Special) {
                            #warning Need access to sequence from enumerator
                        } else {
                            value = Manager.GetObjectMapper().DeserializeKey<object>(enumerator->value);
                        }

                        valsToReduce.Add(value);
                    }

                    lastKey = key;
                }
            } catch(CouchbaseLiteException) {
                Log.W("Failed to run reduce query for {0}", Name);
                throw;
            } catch(Exception e) {
                throw new CouchbaseLiteException(String.Format("Error running reduce query for {0}", Name), e)
                { Code = StatusCode.Exception };
            } finally {
                Native.c4queryenum_free(enumerator);
            }
        }

        public IQueryRowStore StorageForQueryRow(QueryRow row)
        {
            return this;
        }

        public IEnumerable<IDictionary<string, object>> Dump()
        {
            var index = OpenIndex();
            #warning Need to include sequence
            var enumerator = QueryEnumeratorWithOptions(new QueryOptions());
            while (Native.c4queryenum_next(enumerator, null)) {
                yield return new Dictionary<string, object> {
                    { "key", Native.c4key_toJSON(&enumerator->key) },
                    { "value", Native.c4key_toJSON(&enumerator->value) },
                    { "sequence", 0L } 
                };
            }

            Native.c4queryenum_free(enumerator);
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
            throw new NotImplementedException("Need a way to retrieve document by sequence");
        }
    }
}


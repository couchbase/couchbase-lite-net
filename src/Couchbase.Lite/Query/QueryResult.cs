// 
// QueryResult.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Query;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed unsafe class QueryResult : IResult
    {
        #region Variables

        private FLArrayIterator _columns;
        private readonly QueryResultSet _rs;
        private readonly MContext _context;

        #endregion

        #region Properties

        public int Count => _rs.ColumnNames.Count;

        public ReadOnlyFragment this[int index]
        {
            get {
                if (index >= Count) {
                    return ReadOnlyFragment.Null;
                }

                return new ReadOnlyFragment(this, index);
            }
        }

        public ReadOnlyFragment this[string key] => this[IndexForColumnName(key)];

        public ICollection<string> Keys => _rs.ColumnNames.Keys;

        private Database Database
        {
            get {
                var database = _rs.Database;
                Debug.Assert(database != null);
                return database;
            }
        }

        #endregion

        #region Constructors

        internal QueryResult(QueryResultSet rs, C4QueryEnumerator* e, MContext context)
        {
            _rs = rs;
            _columns = e->columns;
            _context = context;
        }

        #endregion

        #region Private Methods

        private object FleeceValueToObject(int index)
        {
            var value = FLValueAtIndex(index);
            if (value == null) {
                return null;
            }

            var root = new MRoot(_context, value, false);
            return root.AsObject();
        }

        private FLValue* FLValueAtIndex(int index)
        {
            fixed (FLArrayIterator* columns = &_columns) {
                return Native.FLArrayIterator_GetValueAt(columns, (uint) index);
            }
        }

        private int IndexForColumnName(string columnName)
        {
            int index;
            if (_rs.ColumnNames.TryGetValue(columnName, out index)) {
                return index;
            }

            return -1;
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<object>) this).GetEnumerator();
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,object>>

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            foreach (var column in _rs.ColumnNames.Keys) {
                yield return new KeyValuePair<string, object>(column, GetObject(column));
            }
        }

        #endregion

        #region IEnumerable<object>

        IEnumerator<object> IEnumerable<object>.GetEnumerator()
        {
            for (var i = 0; i < Count; i++) {
                yield return GetObject(i);
            }
        }

        #endregion

        #region IReadOnlyArray

        public IReadOnlyArray GetArray(int index)
        {
            return FleeceValueToObject(index) as IReadOnlyArray;
        }

        public Blob GetBlob(int index)
        {
            return FleeceValueToObject(index) as Blob;  
        }

        public bool GetBoolean(int index)
        {
            return Native.FLValue_AsBool(FLValueAtIndex(index));
        }

        public DateTimeOffset GetDate(int index)
        {
            return DataOps.ConvertToDate(GetObject(index));
        }

        public IReadOnlyDictionary GetDictionary(int index)
        {
            return FleeceValueToObject(index) as IReadOnlyDictionary;
        }

        public double GetDouble(int index)
        {
            return Native.FLValue_AsDouble(FLValueAtIndex(index));
        }

        public float GetFloat(int index)
        {
            return Native.FLValue_AsFloat(FLValueAtIndex(index));
        }

        public int GetInt(int index)
        {
            return (int)Native.FLValue_AsInt(FLValueAtIndex(index));
        }

        public long GetLong(int index)
        {
            return Native.FLValue_AsInt(FLValueAtIndex(index));
        }

        public object GetObject(int index)
        {
            return FleeceValueToObject(index);
        }

        public string GetString(int index)
        {
            return Native.FLValue_AsString(FLValueAtIndex(index));
        }

        public IList<object> ToList()
        {
            var array = new List<object>();
            for (int i = 0; i < Count; i++) {
                array.Add(FLValueConverter.ToCouchbaseObject(FLValueAtIndex(i), Database, true));
            }

            return array;
        }

        #endregion

        #region IReadOnlyDictionary

        public bool Contains(string key)
        {
            return IndexForColumnName(key) >= 0;
        }

        public IReadOnlyArray GetArray(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 ? GetArray(index) : null;
        }

        public Blob GetBlob(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 ? GetBlob(index) : null;
        }

        public bool GetBoolean(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 && GetBoolean(index);
        }

        public DateTimeOffset GetDate(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 ? GetDate(index) : DateTimeOffset.MinValue;
        }

        public IReadOnlyDictionary GetDictionary(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 ? GetDictionary(index) : null;
        }

        public double GetDouble(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 ? GetDouble(index) : 0.0;
        }

        public float GetFloat(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 ? GetFloat(index) : 0.0f;
        }

        public int GetInt(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 ? GetInt(index) : 0;
        }

        public long GetLong(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 ? GetLong(index) : 0L;
        }

        public object GetObject(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 ? GetObject(index) : null;
        }

        public string GetString(string key)
        {
            var index = IndexForColumnName(key);
            return index >= 0 ? GetString(index) : null;
        }

        public IDictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();
            foreach (var key in Keys) {
                dict[key] = GetObject(key);
            }

            return dict;
        }

        #endregion
    }
}
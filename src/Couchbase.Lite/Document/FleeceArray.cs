// 
// FleeceArray.cs
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
using System.Collections.Generic;
using System.Globalization;

using Couchbase.Lite.Internal.DB;
using Couchbase.Lite.Internal.Serialization;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class FleeceArray : IReadOnlyArray
    {
        #region Variables

        private readonly FLArray* _array;
        private readonly Database _database;
        private readonly C4Document* _document;
        private readonly SharedStringCache _sharedKeys;

        #endregion

        #region Properties

        public int Count => (int) Native.FLArray_Count(_array);

        public IReadOnlyFragment this[int index]
        {
            get {
                var value = index >= 0 && index < Count ? GetObject(index) : null;
                return new ReadOnlyFragment(value);
            }
        }

        #endregion

        #region Constructors

        public FleeceArray()
        {
            
        }

        public FleeceArray(FLArray* array, C4Document* document, IDatabase database)
        {
            var db = database as Database ?? throw new InvalidOperationException("Custom IDatabase not supported");
            _array = array;
            _document = document;
            _database = db;
            _sharedKeys = db.SharedStrings;
        }

        #endregion

        #region IReadOnlyArray

        public IReadOnlyArray GetArray(int index)
        {
            return GetObject(index) as IReadOnlyArray;
        }

        public IBlob GetBlob(int index)
        {
            return GetObject(index) as IBlob;
        }

        public bool GetBoolean(int index)
        {
            return Native.FLValue_AsBool(Native.FLArray_Get(_array, (uint)index));
        }

        public DateTimeOffset GetDate(int index)
        {
            var dateString = GetString(index);
            if (dateString == null) {
                return DateTimeOffset.MinValue;
            }

            return DateTimeOffset.ParseExact(dateString, "o", CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        public double GetDouble(int index)
        {
            return Native.FLValue_AsDouble(Native.FLArray_Get(_array, (uint)index));
        }

        public int GetInt(int index)
        {
            return (int)GetLong(index);
        }

        public long GetLong(int index)
        {
            return Native.FLValue_AsInt(Native.FLArray_Get(_array, (uint) index));
        }

        public object GetObject(int index)
        {
            return FLValueConverter.ToCouchbaseObject(Native.FLArray_Get(_array, (uint)index), _sharedKeys, _document, _database);
        }

        public string GetString(int index)
        {
            return Native.FLValue_AsString(Native.FLArray_Get(_array, (uint) index));
        }

        public IReadOnlySubdocument GetSubdocument(int index)
        {
            return GetObject(index) as IReadOnlySubdocument;
        }

        public IList<object> ToList()
        {
            var array = new List<object>(Count);
            for (int i = 0; i < Count; i++) {
                var value = Native.FLArray_Get(_array, (uint) i);
                array.Add(FLValueConverter.ToObject(value, _sharedKeys));
            }

            return array;
        }

        #endregion
    }
}

// 
// ReadOnlyArray.cs
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
using System.Linq;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using LiteCore.Interop;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a readonly ordered collection of objects
    /// </summary>
    public unsafe class ReadOnlyArray : IReadOnlyArray
    {
        private readonly FLArray* _array;
        private readonly SharedStringCache _sharedKeys;

        #region Properties

        /// <inheritdoc />
        public virtual int Count => (int) Native.FLArray_Count(_array);

        /// <inheritdoc />
        public ReadOnlyFragment this[int index]
        {
            get {
                var value = index >= 0 && index < Count ? GetObject(index) : null;
                return new ReadOnlyFragment(value);
            }
        }

        internal FleeceArray Data { get; set; }

        #endregion

        #region Constructors

        internal ReadOnlyArray(FleeceArray data)
        {
            Data = data;
            _array = data != null ? data.Array : null;
            _sharedKeys = data?.Database?.SharedStrings;
        }

        #endregion

        private object FleeceValueToObject(int index)
        {
            var value = Native.FLArray_Get(_array, (uint) index);
            if (value != null) {
                var c4Doc = Data != null ? Data.C4Doc : null;
                return FLValueConverter.ToCouchbaseObject(value, _sharedKeys, c4Doc, Data?.Database);
            }

            return null;
        }

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<object>

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public virtual IEnumerator<object> GetEnumerator()
        {
            if (Data == null) {
                yield break;
            }

            foreach (var item in Data) {
                yield return item;
            }
        }

        #endregion

        #region IReadOnlyArray

        /// <inheritdoc />
        public IReadOnlyArray GetArray(int index)
        {
            return FleeceValueToObject(index) as IReadOnlyArray;
        }

        /// <inheritdoc />
        public virtual Blob GetBlob(int index)
        {
            return FleeceValueToObject(index) as Blob;
        }

        /// <inheritdoc />
        public virtual bool GetBoolean(int index)
        {
            return Native.FLValue_AsBool(Native.FLArray_Get(_array, (uint) index));
        }

        /// <inheritdoc />
        public virtual DateTimeOffset GetDate(int index)
        {
            return DataOps.ConvertToDate(FleeceValueToObject(index) as string);
        }

        /// <inheritdoc />
        public virtual double GetDouble(int index)
        {
            return Native.FLValue_AsDouble(Native.FLArray_Get(_array, (uint)index));
        }

        /// <inheritdoc />
        public virtual int GetInt(int index)
        {
            return (int)Native.FLValue_AsInt(Native.FLArray_Get(_array, (uint)index));
        }

        /// <inheritdoc />
        public virtual long GetLong(int index)
        {
            return Native.FLValue_AsInt(Native.FLArray_Get(_array, (uint)index));
        }

        /// <inheritdoc />
        public virtual object GetObject(int index)
        {
            return FleeceValueToObject(index);
        }

        /// <inheritdoc />
        public virtual string GetString(int index)
        {
            return FleeceValueToObject(index) as string;
        }

        /// <inheritdoc />
        public IReadOnlyDictionary GetDictionary(int index)
        {
            return FleeceValueToObject(index) as IReadOnlyDictionary;
        }

        /// <inheritdoc />
        public virtual IList<object> ToList()
        {
            return Data.ToList();
        }

        #endregion
    }
}

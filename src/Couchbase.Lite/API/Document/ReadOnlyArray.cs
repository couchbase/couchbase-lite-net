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
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Support;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a readonly ordered collection of objects
    /// </summary>
    public class ReadOnlyArray : IReadOnlyArray
    {
        #region Variables

        internal readonly MArray _array = new MArray();
        internal readonly ThreadSafety _threadSafety = new ThreadSafety();

        #endregion

        #region Properties

        /// <inheritdoc />
        public virtual int Count => _array.Count;

        /// <inheritdoc />
        public ReadOnlyFragment this[int index] => index >= _array.Count ? ReadOnlyFragment.Null : new ReadOnlyFragment(this, index);

        #endregion

        #region Constructors

        internal ReadOnlyArray()
        {
        }

        internal ReadOnlyArray(MArray array, bool isMutable)
        {
            _array.InitAsCopyOf(array, isMutable);
        }

        internal ReadOnlyArray(ReadOnlyArray original, bool mutable)
            : this(original._array, mutable)
        {
            
        }

        internal ReadOnlyArray(MValue mv, MCollection parent)
        {
            _array.InitInSlot(mv, parent);
        }

        #endregion

        #region Internal Methods

        internal MCollection ToMCollection()
        {
            return _array;
        }

        internal ArrayObject ToMutable()
        {
            return new ArrayObject(_array, true);
        }

        #endregion

        #region Private Methods

        private static MValue Get(MArray array, int index, IThreadSafety threadSafety = null)
        {
            return (threadSafety ?? NullThreadSafety.Instance).DoLocked(() =>
            {
                var val = array.Get(index);
                if (val.IsEmpty) {
                    throw new IndexOutOfRangeException();
                }

                return val;
            });
        }

        private static object GetObject(MArray array, int index, IThreadSafety threadSafety = null) => Get(array, index, threadSafety).AsObject(array);

        private static T GetObject<T>(MArray array, int index, IThreadSafety threadSafety = null) where T : class => GetObject(array, index, threadSafety) as T;

        #endregion

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
        public virtual IEnumerator<object> GetEnumerator() => _array.GetEnumerator();

        #endregion

        #region IReadOnlyArray

        /// <inheritdoc />
        public IReadOnlyArray GetArray(int index) => GetObject<IReadOnlyArray>(_array, index, _threadSafety);

        /// <inheritdoc />
        public Blob GetBlob(int index) => GetObject<Blob>(_array, index, _threadSafety);

        /// <inheritdoc />
        public bool GetBoolean(int index) => DataOps.ConvertToBoolean(GetObject(_array, index, _threadSafety));

        /// <inheritdoc />
        public DateTimeOffset GetDate(int index) => DataOps.ConvertToDate(GetObject(_array, index, _threadSafety));

        /// <inheritdoc />
        public IReadOnlyDictionary GetDictionary(int index) => GetObject<IReadOnlyDictionary>(_array, index, _threadSafety);

        /// <inheritdoc />
        public double GetDouble(int index) => DataOps.ConvertToDouble(GetObject(_array, index, _threadSafety));

        /// <inheritdoc />
        public float GetFloat(int index) => DataOps.ConvertToFloat(GetObject(_array, index, _threadSafety));

        /// <inheritdoc />
        public int GetInt(int index) => DataOps.ConvertToInt(GetObject(_array, index, _threadSafety));

        /// <inheritdoc />
        public long GetLong(int index) => DataOps.ConvertToLong(GetObject(_array, index, _threadSafety));

        /// <inheritdoc />
        public object GetObject(int index) => GetObject(_array, index, _threadSafety);

        /// <inheritdoc />
        public string GetString(int index) => GetObject<string>(_array, index, _threadSafety);

        /// <inheritdoc />
        public List<object> ToList()
        {
            var count = _array.Count;
            var result = new List<object>(count);
            _threadSafety.DoLocked(() =>
            {
                for (var i = 0; i < count; i++) {
                    result.Add(DataOps.ToNetObject(GetObject(_array, i)));
                }
            });

            return result;
        }

        #endregion
    }
}

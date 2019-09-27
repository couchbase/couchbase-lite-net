// 
//  MutableArrayObject.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using System;
using System.Collections;

using Couchbase.Lite.Fleece;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing an editable collection of objects
    /// </summary>
    public sealed class MutableArrayObject : ArrayObject, IMutableArray
    {
        #region Properties

        /// <summary>
        /// Gets a fragment style entry from the array by index
        /// </summary>
        /// <param name="index">The index to retrieve</param>
        /// <returns>The fragment of the object at the index</returns>
        public new IMutableFragment this[int index] => index >= _array.Count
            ? Fragment.Null
            : new Fragment(this, index);

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public MutableArrayObject()
        {
            
        }

        /// <summary>
        /// Creates an array with the given data
        /// </summary>
        /// <param name="array">The data to populate the array with</param>
        public MutableArrayObject(IList array)
            : this()
        {
            SetData(array);
        }

        internal MutableArrayObject(FleeceMutableArray array, bool isMutable)
        {
            _array.InitAsCopyOf(array, isMutable);
        }

        internal MutableArrayObject(MValue mv, MCollection parent)
            : base(mv, parent)
        {
            
        }

        #endregion

        #region Private Methods

        private void SetValueInternal(int index, object value)
        {
            _threadSafety.DoLocked(() =>
            {
                if (DataOps.ValueWouldChange(value, _array.Get(index), _array)) {
                    _array.Set(index, value);
                }
            });
        }

        #endregion

        #region Overrides

        internal override ArrayObject ToImmutable()
        {
            return new ArrayObject(_array, false);
        }

        #endregion

        #region IMutableArray

        /// <inheritdoc />
        public IMutableArray AddValue(object value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray AddString(string value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray AddInt(int value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray AddLong(long value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray AddFloat(float value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray AddDouble(double value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray AddBoolean(bool value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray AddBlob(Blob value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray AddDate(DateTimeOffset value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray AddArray(ArrayObject value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray AddDictionary(DictionaryObject value)
        {
            _threadSafety.DoLocked(() => _array.Add(value));
            return this;
        }

        /// <inheritdoc />
        public new MutableArrayObject GetArray(int index)
        {
            return base.GetArray(index) as MutableArrayObject;
        }

        /// <inheritdoc />
        public new MutableDictionaryObject GetDictionary(int index)
        {
            return base.GetDictionary(index) as MutableDictionaryObject;
        }

        /// <inheritdoc />
        public IMutableArray InsertValue(int index, object value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray InsertString(int index, string value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray InsertInt(int index, int value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray InsertLong(int index, long value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray InsertFloat(int index, float value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray InsertDouble(int index, double value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray InsertBoolean(int index, bool value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray InsertBlob(int index, Blob value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray InsertDate(int index, DateTimeOffset value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray InsertArray(int index, ArrayObject value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray InsertDictionary(int index, DictionaryObject value)
        {
            _threadSafety.DoLocked(() => _array.Insert(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray RemoveAt(int index)
        {
            _threadSafety.DoLocked(() => _array.RemoveAt(index));
            
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetData(IList array)
        {
            _threadSafety.DoLocked(() =>
            {
                _array.Clear();
                if (array != null) {
                    foreach (var item in array) {
                        _array.Add(DataOps.ToCouchbaseObject(item));
                    }
                }
            });

            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetValue(int index, object value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetString(int index, string value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetInt(int index, int value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetLong(int index, long value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetFloat(int index, float value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetDouble(int index, double value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetBoolean(int index, bool value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetBlob(int index, Blob value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetDate(int index, DateTimeOffset value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetArray(int index, ArrayObject value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        /// <inheritdoc />
        public IMutableArray SetDictionary(int index, DictionaryObject value)
        {
            _threadSafety.DoLocked(() => SetValueInternal(index, value));
            return this;
        }

        #endregion
    }
}

// 
// ArrayObject.cs
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
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing an editable collection of objects
    /// </summary>
    public sealed class MutableArray : ArrayObject, IMutableArray
    {
        #region Properties

        /// <inheritdoc />
        public new MutableFragment this[int index] => index >= _array.Count
            ? MutableFragment.Null
            : new MutableFragment(this, index);

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public MutableArray()
        {
            
        }

        /// <summary>
        /// Creates an array with the given data
        /// </summary>
        /// <param name="array">The data to populate the array with</param>
        public MutableArray(IList array)
            : this()
        {
            Set(array);
        }

        internal MutableArray(MArray array, bool isMutable)
        {
            _array.InitAsCopyOf(array, isMutable);
        }

        internal MutableArray(MValue mv, MCollection parent)
            : base(mv, parent)
        {
            
        }

        #endregion

        #region Private Methods

        private void SetValue(int index, object value)
        {
            _threadSafety.DoLocked(() =>
            {
                if (DataOps.ValueWouldChange(value, _array.Get(index), _array)) {
                    _array.Set(index, DataOps.ToCouchbaseObject(value));
                }
            });
        }

        #endregion

        #region IMutableArray

        /// <inheritdoc />
        public IMutableArray Add(object value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(DataOps.ToCouchbaseObject(value));
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Add(string value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Add(int value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Add(long value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Add(float value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Add(double value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Add(bool value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Add(Blob value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Add(DateTimeOffset value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(value.ToString("o"));
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Add(MutableArray value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Add(DictionaryObject value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Add(value);
                return this;
            });
        }

        /// <inheritdoc />
        public new IMutableArray GetArray(int index)
        {
            return base.GetArray(index) as IMutableArray;
        }

        /// <inheritdoc />
        public new IMutableDictionary GetDictionary(int index)
        {
            return base.GetDictionary(index) as IMutableDictionary;
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, object value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, DataOps.ToCouchbaseObject(value));
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, string value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, int value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, long value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, float value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, double value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, bool value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, Blob value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, DateTimeOffset value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, value.ToString("o"));
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, MutableArray value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Insert(int index, DictionaryObject value)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Insert(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray RemoveAt(int index)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.RemoveAt(index);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(IList array)
        {
            return _threadSafety.DoLocked(() =>
            {
                _array.Clear();
                foreach (var item in array) {
                    _array.Add(DataOps.ToCouchbaseObject(item));
                }

                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, object value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, string value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, int value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, long value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, float value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, double value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, bool value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, Blob value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, DateTimeOffset value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, MutableArray value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        /// <inheritdoc />
        public IMutableArray Set(int index, DictionaryObject value)
        {
            return _threadSafety.DoLocked(() =>
            {
                SetValue(index, value);
                return this;
            });
        }

        #endregion
    }
}

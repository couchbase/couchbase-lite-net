// 
// MArray.cs
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
using System.Linq;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Serialization
{
    internal sealed unsafe class MArray : MCollection, IList<object>
    {
        #region Variables

        private List<MValue> _vec = new List<MValue>();

        #endregion

        #region Properties

        public FLArray* BaseArray { get; private set; }

        public int Count => _vec.Count;

        public bool IsReadOnly => !IsMutable;

        public object this[int index]
        {
            get => Get(index).AsObject(this);
            set => Set(index, value);
        }

        #endregion

        #region Constructors

        public MArray()
        {
            
        }

        public MArray(MValue mv, MCollection parent)
        {
            InitInSlot(mv, parent);
        }

        #endregion

        #region Public Methods

        public MValue Get(int index)
        {
            if (index < 0 || index >= _vec.Count) {
                return MValue.Empty;
            }

            var val = _vec[index];
            if (val.IsEmpty) {
                val = new MValue(Native.FLArray_Get(BaseArray, (uint) index));
                _vec[index] = val;
            }

            return val;
        }

        public void InitInSlot(MValue mv, MCollection parent)
        {
            InitInSlot(mv, parent, parent?.MutableChildren ?? false);
        }

        public void RemoveRange(int start, int count = 1)
        {
            if (!IsMutable) {
                throw new InvalidOperationException("Cannot remove items from a non-mutable MArray");
            }

            var end = start + count;
            if (end == start) {
                return;
            }

            if (start < 0) {
                throw new ArgumentOutOfRangeException($"Cannot remove starting from an index less than 0 (got {start})");
            }

            if (end < start) {
                throw new ArgumentOutOfRangeException($"Cannot remove a range that ends before it starts (got start={start}, count = {count})");
            }

            if (end > _vec.Count) {
                throw new ArgumentOutOfRangeException($"Range end for remove exceeds the length of the array (got start={start}, count = {count})");
            }

            if (end < Count) {
                PopulateVec();
            }

            Mutate();
            _vec.RemoveRange(start, count);
        }

        public void Set(int index, object val)
        {
            if (!IsMutable) {
                throw new InvalidOperationException("Cannot set items in a non-mutable MArray");
            }

            if (index < 0 || index >= Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (val == null) {
                throw new ArgumentNullException(nameof(val));
            }

            Mutate();
            _vec[index] = new MValue(val);
        }

        #endregion

        #region Private Methods

        private void PopulateVec()
        {
            for (int i = 0; i < _vec.Count; i++) {
                var v = _vec[i];
                if (v.IsEmpty) {
                    _vec[i] = new MValue(Native.FLArray_Get(BaseArray, (uint) i));
                }
            }
        }

        private void Resize(int newSize)
        {
            var count = _vec.Count;
            if (newSize < count) {
                _vec.RemoveRange(newSize, count - newSize);
            } else if (newSize > count) {
                if (newSize > _vec.Capacity) {
                    _vec.Capacity = newSize;
                }

                _vec.AddRange(Enumerable.Repeat(MValue.Empty, newSize - count));
            }
        }

        #endregion

        #region Overrides

        public override void InitAsCopyOf(MCollection original, bool isMutable)
        {
            var a = original as MArray;
            base.InitAsCopyOf(original, isMutable);
            BaseArray = a != null ? a.BaseArray : null;
            _vec = a?._vec;
        }

        protected override void InitInSlot(MValue slot, MCollection parent, bool isMutable)
        {
            base.InitInSlot(slot, parent, isMutable);
            Debug.Assert(BaseArray == null);
            BaseArray = Native.FLValue_AsArray(slot.Value);
            Resize((int)Native.FLArray_Count(BaseArray));
        }

        #endregion

        #region ICollection<object>

        public void Add(object item)
        {
            Insert(Count, item);
        }

        public void Clear()
        {
            if (!IsMutable) {
                throw new InvalidOperationException("Cannot clear a non-mutable MArray");
            }

            if (!_vec.Any()) {
                return;
            }

            Mutate();
            _vec.Clear();
        }

        public bool Contains(object item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(object[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(object item)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<object>

        public IEnumerator<object> GetEnumerator()
        {
            return new Enumerator(this);
        }

        #endregion

        #region IFLEncodable

        public override void FLEncode(FLEncoder* enc)
        {
            if (!IsMutated) {
                if (BaseArray == null) {
                    Native.FLEncoder_BeginArray(enc, 0UL);
                    Native.FLEncoder_EndArray(enc);
                } else {
                    Native.FLEncoder_WriteValue(enc, (FLValue*) BaseArray);
                }
            } else {
                Native.FLEncoder_BeginArray(enc, (uint)Count);
                uint i = 0;
                foreach (var item in _vec) {
                    if (item.IsEmpty) {
                        Native.FLEncoder_WriteValue(enc, Native.FLArray_Get(BaseArray, i));
                    } else {
                        item.FLEncode(enc);
                    }

                    ++i;
                }

                Native.FLEncoder_EndArray(enc);
            }
        }

        #endregion

        #region IList<object>

        public int IndexOf(object item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, object val)
        {
            if (!IsMutable) {
                throw new InvalidOperationException("Cannot insert items in a non-mutable MArray");
            }

            if (index < 0 || index > Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (val == null) {
                throw new ArgumentNullException(nameof(val));
            }

            if (index < Count) {
                PopulateVec();
            }

            Mutate();
            _vec.Insert(index, new MValue(val));
        }

        public void RemoveAt(int index)
        {
            RemoveRange(index);
        }

        #endregion

        #region Nested

        private class Enumerator : IEnumerator<object>
        {
            #region Variables

            private readonly MArray _parent;
            private int _current = -1;

            #endregion

            #region Properties

            public object Current => _parent[_current];

            object IEnumerator.Current => Current;

            #endregion

            #region Constructors

            public Enumerator(MArray parent)
            {
                _parent = parent;
            }

            #endregion

            #region IDisposable

            public void Dispose()
            {
                
            }

            #endregion

            #region IEnumerator

            public bool MoveNext()
            {
                ++_current;
                return _current < _parent.Count;
            }

            public void Reset()
            {
                _current = -1;
            }

            #endregion
        }

        #endregion
    }
}
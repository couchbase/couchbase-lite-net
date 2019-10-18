﻿// 
// FleeceMutableArray.cs
// 
// Copyright (c) 2019 Couchbase, Inc All rights reserved.
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

using LiteCore.Interop;
using Couchbase.Lite.Internal.Serialization;

using JetBrains.Annotations;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Fleece
{
    internal sealed unsafe class FleeceMutableArray : MCollection, IList<object>
    {
        #region Constants

        private const string Tag = nameof(FleeceMutableArray);

        #endregion

        #region Variables

        [NotNull]
        [ItemNotNull]
        private List<MValue> _vec = new List<MValue>();

        private FLMutableArray* _flArr;

        private bool _releaseRequired = false;

        #endregion

        #region Properties

        public int Count => (int)Native.FLArray_Count((FLArray*)_flArr);

        public bool IsReadOnly => !IsMutable;

        public bool IsEmpty => Native.FLArray_IsEmpty((FLArray*)_flArr);

        //public bool IsMutated => Native.FLMutableArray_IsChanged(ar);

        public object this[int index]
        {
            get => Get(index).AsObject(this);
            set => Set(index, value);
        }

        #endregion

        #region Constructors

        public FleeceMutableArray()
        {

        }

        public FleeceMutableArray(MValue mv, MCollection parent)
        {
            InitInSlot(mv, parent);
        }

        #endregion

        #region Public Methods

        [NotNull]
        public MValue Get(int index)
        {
            if (index < 0 || index >= _vec.Count) {
                return MValue.Empty;
            }

            var val = _vec[index];
            if (val.IsEmpty) {
                val = new MValue(Native.FLArray_Get((FLArray*)_flArr, (uint)index));
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
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.CannotRemoveItemsFromNonMutableMArray);
            }

            var end = start + count;
            if (end == start) {
                return;
            }

            if (start < 0) {
                throw new ArgumentOutOfRangeException(String.Format(CouchbaseLiteErrorMessage.CannotRemoveStartingFromIndexLessThan, start));
            }

            if (end < start) {
                throw new ArgumentOutOfRangeException(String.Format(CouchbaseLiteErrorMessage.CannotRemoveRangeEndsBeforeItStarts, start, count));
            }

            if (end > _vec.Count) {
                throw new ArgumentOutOfRangeException(String.Format(CouchbaseLiteErrorMessage.RangeEndForRemoveExceedsArrayLength, start, count));
            }

            if (end < Count) {
                PopulateArr();
            }

            Mutate();
            Native.FLMutableArray_Remove(_flArr, (uint)start, (uint)count);
            _vec.RemoveRange(start, count);
        }

        public void Set(int index, object val)
        {
            if (!IsMutable) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.CannotSetItemsInNonMutableMArray);
            }

            if (index < 0 || index >= Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (val == null) {
                throw new ArgumentNullException(nameof(val));
            }

            SetValue(index, val);
            _vec[index] = new MValue(val);
        }

        #endregion

        #region Internal Method(s)

        internal void NewFLMutableArray()
        {
            _flArr = Native.FLMutableArray_New();
            _releaseRequired = true;
        }
        #endregion

        #region Private Methods

        private void PopulateArr()
        {
            for (int i = 0; i < _vec.Count; i++) {
                var v = _vec[i];
                if (v.IsEmpty) {
                    var val = Native.FLArray_Get((FLArray*)_flArr, (uint)i);
                    _vec[i] = new MValue(val);
                }
            }
        }

        private void Resize(int newSize)
        {
            Native.FLMutableArray_Resize(_flArr, (uint)newSize);
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

        private void SetValue(int index, object val, bool isInserting = false)
        {
            if (index < Count) {
                PopulateArr();
            }

            Mutate();
            using (var encoded = val.FLEncode()) {
                //Convert object into FLValue
                var flValue = NativeRaw.FLValue_FromData((FLSlice)encoded, FLTrust.Trusted);
                if (isInserting) {
                    Native.FLMutableArray_Insert(_flArr, (uint)index, 1);
                }

                Native.FLSlot_SetValue(Native.FLMutableArray_Set(_flArr, (uint)index), flValue);
                //TODO:
                //This will probably need to be reworked later. There is a whole suite of 
                //FLSlot_Set* functions that are available and can be used by writing a method 
                //similar to FLEncode() that calls the appropriate FLSlot_Set* methods. 
                //This avoids encoding upfront.
            }
        }

        #endregion

        #region Overrides

        public override void InitAsCopyOf(MCollection original, bool isMutable)
        {
            var a = original as FleeceMutableArray;
            base.InitAsCopyOf(original, isMutable);
            _flArr = a != null ? a._flArr : null;
            _vec = a?._vec ?? new List<MValue>();
        }

        protected override void InitInSlot(MValue slot, MCollection parent, bool isMutable)
        {
            base.InitInSlot(slot, parent, isMutable);
            var b = Native.FLValue_AsArray(slot.Value);
            _flArr = Native.FLArray_MutableCopy(b, FLCopyFlags.DefaultCopy);
            Resize((int)Native.FLArray_Count((FLArray*)_flArr));
            _releaseRequired = true;
        }

        #endregion

        #region ICollection<object>

        //append the item at the end of array
        public void Add(object item)
        {
            Insert(Count, item);
        }

        public void Clear()
        {
            if (!IsMutable) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.CannotClearNonMutableMArray);
            }

            if (!_vec.Any()) {
                return;
            }

            Mutate();
            Native.FLMutableArray_Remove(_flArr, 0, (uint)Count);
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

        #region IList<object>

        public int IndexOf(object item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, object val)
        {
            if (!IsMutable) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.CannotInsertItemsInNonMutableMArray);
            }

            if (index < 0 || index > _vec.Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            SetValue(index, val, true);
            _vec.Insert(index, new MValue(val));
        }

        public void RemoveAt(int index)
        {
            RemoveRange(index);
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
                if ((FLArray*)_flArr == null) {
                    Native.FLEncoder_BeginArray(enc, 0UL);
                    Native.FLEncoder_EndArray(enc);
                } else {
                    Native.FLEncoder_WriteValue(enc, (FLValue*)(FLArray*)_flArr);
                }
            } else {
                Native.FLEncoder_BeginArray(enc, (uint)Count);
                for (int i = 0; i < Count; i++) {
                    var item = Get(i);
                    if (item.IsEmpty) {
                        Native.FLEncoder_WriteValue(enc, Native.FLArray_Get((FLArray*)_flArr, (uint)i));
                    } else {
                        item.NativeObject.FLEncode(enc);
                    }
                }

                Native.FLEncoder_EndArray(enc);
            }
            
        }

        #endregion

        #region IDisposable

        public override void Dispose()
        {
            base.Dispose();
            if (_releaseRequired && _flArr != null) {
                Native.FLValue_Release((FLValue*)_flArr);
                _flArr = null;
                _releaseRequired = false;
            }
        }

        #endregion
    }

    #region Nested

    unsafe class Enumerator : IEnumerator<object>
    {
        #region Variables 

        private readonly FleeceMutableArray _parent;
        private int _current = -1;

        #endregion

        #region Properties

        public object Current => _parent[_current];

        object IEnumerator.Current => Current;

        #endregion

        #region Constructors

        public Enumerator(FleeceMutableArray parent)
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

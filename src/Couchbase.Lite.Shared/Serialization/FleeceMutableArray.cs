// 
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Couchbase.Lite.Fleece;

internal sealed unsafe class FleeceMutableArray : MCollection, IList<object?>
{
    private List<MValue> _vec = new List<MValue>();

    public FLArray* BaseArray { get; private set; }

    public int Count => _vec.Count;

    public bool IsReadOnly => !IsMutable;

    public object? this[int index]
    {
        get => Get(index).AsObject(this);
        set => Set(index, value);
    }

    public FleeceMutableArray()
    {

    }

    public FleeceMutableArray(MValue mv, MCollection? parent)
    {
        InitInSlot(mv, parent);
    }

    public MValue Get(int index)
    {
        Debug.Assert(Context != null);
        Context!.CheckDisposed();
        if (index < 0 || index >= _vec.Count) {
            return MValue.Empty;
        }

        var val = _vec[index];
        if (val.IsEmpty) {
            val = new MValue(Native.FLArray_Get(BaseArray, (uint)index));
            _vec[index] = val;
        }

        return val;
    }

    public void InitInSlot(MValue mv, MCollection? parent)
    {
        InitInSlot(mv, parent, parent?.MutableChildren ?? false);
    }

    public void RemoveRange(int start, int count = 1)
    {
        Debug.Assert(Context != null);
        Context!.CheckDisposed();
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
            PopulateVec();
        }

        Mutate();
        _vec.RemoveRange(start, count);
    }

    public void Set(int index, object? val)
    {
        Debug.Assert(Context != null);
        Context!.CheckDisposed();
        if (!IsMutable) {
            throw new InvalidOperationException(CouchbaseLiteErrorMessage.CannotSetItemsInNonMutableMArray);
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

    private void PopulateVec()
    {
        for (int i = 0; i < _vec.Count; i++) {
            var v = _vec[i];
            if (v.IsEmpty) {
                _vec[i] = new MValue(Native.FLArray_Get(BaseArray, (uint)i));
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

    public override void InitAsCopyOf(MCollection original, bool isMutable)
    {
        var a = original as FleeceMutableArray;
        base.InitAsCopyOf(original, isMutable);
        BaseArray = a != null ? a.BaseArray : null;
        _vec = a?._vec ?? new List<MValue>();
    }

    protected override void InitInSlot(MValue slot, MCollection? parent, bool isMutable)
    {
        base.InitInSlot(slot, parent, isMutable);
        Debug.Assert(BaseArray == null);
        BaseArray = Native.FLValue_AsArray(slot.Value);
        Resize((int)Native.FLArray_Count(BaseArray));
    }

    public void Add(object? item)
    {
        Insert(Count, item);
    }

    public void Clear()
    {
        Debug.Assert(Context != null);
        Context!.CheckDisposed();
        if (!IsMutable) {
            throw new InvalidOperationException(CouchbaseLiteErrorMessage.CannotClearNonMutableMArray);
        }

        if (!_vec.Any()) {
            return;
        }

        Mutate();
        _vec.Clear();
    }

    public bool Contains(object? item)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(object?[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    public bool Remove(object? item)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<object?> GetEnumerator()
    {
        Debug.Assert(Context != null);
        Context!.CheckDisposed();
        return new Enumerator(this);
    }

    public override void FLEncode(FLEncoder* enc)
    {
        Debug.Assert(Context != null);
        Context!.CheckDisposed();
        if (!IsMutated) {
            if (BaseArray == null) {
                Native.FLEncoder_BeginArray(enc, 0UL);
                Native.FLEncoder_EndArray(enc);
            } else {
                Native.FLEncoder_WriteValue(enc, (FLValue*)BaseArray);
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

    public int IndexOf(object? item) => throw new NotImplementedException();

    public void Insert(int index, object? val)
    {
        Debug.Assert(Context != null);
        Context!.CheckDisposed();
        if (!IsMutable) {
            throw new InvalidOperationException("Cannot insert items in a non-mutable FleeceMutableArray");
        }

        if (index < 0 || index > Count) {
            throw new ArgumentOutOfRangeException(nameof(index));
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

    internal string ToJSON()
    {
        return Native.FLValue_ToJSON((FLValue*)BaseArray)!;
    }

    public override void FLSlotSet(FLSlot* slot)
    {
        if (BaseArray == null) {
            Native.FLSlot_SetNull(slot);
        } else {
            Native.FLSlot_SetValue(slot, (FLValue*)BaseArray);
        }
    }

    private class Enumerator(FleeceMutableArray parent) : IEnumerator<object?>
    {
        private int _current = -1;

        public object? Current => parent[_current];

        object? IEnumerator.Current => Current;

        public void Dispose()
        {

        }

        public bool MoveNext()
        {
            ++_current;
            return _current < parent.Count;
        }

        public void Reset()
        {
            _current = -1;
        }
    }
}
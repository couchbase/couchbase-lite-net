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
using System.Collections.Generic;
using Couchbase.Lite.Fleece;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;

namespace Couchbase.Lite;

/// <summary>
/// A class representing an editable collection of objects
/// </summary>
public sealed class MutableArrayObject : ArrayObject, IMutableArray
{
    /// <summary>
    /// Gets a fragment style entry from the array by index
    /// </summary>
    /// <param name="index">The index to retrieve</param>
    /// <returns>The fragment of the object at the index</returns>
    public new IMutableFragment this[int index] => index >= Array.Count
        ? Fragment.Null
        : new Fragment(this, index);

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

    /// <summary>
    /// Creates an array with the given json string
    /// </summary>
    /// <param name="json">The data to populate the array with</param>
    public MutableArrayObject(string json)
        : this()
    {
        SetJSON(json);
    }

    internal MutableArrayObject(FleeceMutableArray array, bool isMutable)
    {
        Array.InitAsCopyOf(array, isMutable);
    }

    internal MutableArrayObject(MValue mv, MCollection? parent)
        : base(mv, parent)
    {
            
    }

    private void SetValueInternal(int index, object? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        if (DataOps.ValueWouldChange(value, Array.Get(index), Array)) {
            Array.Set(index, DataOps.ToCouchbaseObject(value));
        }
    }

    /// <inheritdoc />
    public IMutableArray AddValue(object? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray AddString(string? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray AddInt(int value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray AddLong(long value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray AddFloat(float value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray AddDouble(double value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray AddBoolean(bool value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray AddBlob(Blob? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray AddDate(DateTimeOffset value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray AddArray(ArrayObject? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray AddDictionary(DictionaryObject? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Add(value);
        return this;
    }

    /// <inheritdoc />
    public new MutableArrayObject? GetArray(int index)
    {
        return base.GetArray(index) as MutableArrayObject;
    }

    /// <inheritdoc />
    public new MutableDictionaryObject? GetDictionary(int index)
    {
        return base.GetDictionary(index) as MutableDictionaryObject;
    }

    /// <inheritdoc />
    public IMutableArray InsertValue(int index, object? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray InsertString(int index, string? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray InsertInt(int index, int value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray InsertLong(int index, long value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray InsertFloat(int index, float value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray InsertDouble(int index, double value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray InsertBoolean(int index, bool value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray InsertBlob(int index, Blob? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray InsertDate(int index, DateTimeOffset value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray InsertArray(int index, ArrayObject? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray InsertDictionary(int index, DictionaryObject? value)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Insert(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray RemoveAt(int index)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.RemoveAt(index);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetData(IList array)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        Array.Clear();
        foreach (var item in array) {
            Array.Add(DataOps.ToCouchbaseObject(item));
        }

        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetValue(int index, object? value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetString(int index, string? value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetInt(int index, int value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetLong(int index, long value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetFloat(int index, float value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetDouble(int index, double value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetBoolean(int index, bool value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetBlob(int index, Blob? value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetDate(int index, DateTimeOffset value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetArray(int index, ArrayObject? value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetDictionary(int index, DictionaryObject? value)
    {
        SetValueInternal(index, value);
        return this;
    }

    /// <inheritdoc />
    public IMutableArray SetJSON(string json)
    {
        var data = DataOps.ParseTo<List<object>>(json);
        return data == null 
            ? throw new ArgumentException("Bad json received in SetJSON", json) 
            : SetData(data);
    }
}
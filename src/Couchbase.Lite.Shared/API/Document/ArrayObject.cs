// 
//  ArrayObject.cs
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
using Couchbase.Lite.Support;
using LiteCore;
using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite;

/// <summary>
/// A class representing a readonly ordered collection of objects
/// </summary>
public class ArrayObject : IArray, IJSON
{
    internal readonly FleeceMutableArray Array = [];

    internal readonly ThreadSafety ThreadSafety;

    /// <inheritdoc />
    public int Count
    {
        get {
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            return Array.Count;
        }
    }

    /// <inheritdoc />
    public IFragment this[int index] => index >= Count ? Fragment.Null : new Fragment(this, index);

    internal ArrayObject()
    {
        ThreadSafety = SetupThreadSafety();
    }

    internal ArrayObject(FleeceMutableArray array, bool isMutable)
    {
        Array.InitAsCopyOf(array, isMutable);
        ThreadSafety = SetupThreadSafety();
    }

    internal ArrayObject(MValue mv, MCollection? parent)
    {
        Array.InitInSlot(mv, parent);
        ThreadSafety = SetupThreadSafety();
    }

    /// <summary>
    /// Similar to the LINQ method, but returns all objects converted to standard
    /// .NET types
    /// </summary>
    /// <returns>A list of standard .NET typed objects in the array</returns>
    public List<object?> ToList()
    {
        var count = Array.Count;
        var result = new List<object?>(count);
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        for (var i = 0; i < count; i++) {
            result.Add(DataOps.ToNetObject(GetObject(Array, i)));
        }

        return result;
    }

    /// <summary>
    /// Creates a copy of this object that can be mutated
    /// </summary>
    /// <returns>A mutable copy of the array</returns>
    public MutableArrayObject ToMutable()
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        return new MutableArrayObject(Array, true);
    }

    internal MCollection ToMCollection()
    {
        return Array;
    }

    private static MValue Get(FleeceMutableArray array, int index, IThreadSafety? threadSafety = null)
    {
        using var threadSafetyScope = threadSafety?.BeginLockedScope();
        var val = array.Get(index);
        return val.IsEmpty ? throw new IndexOutOfRangeException() : val;
    }

    private static object? GetObject(FleeceMutableArray array, int index, IThreadSafety? threadSafety = null) => Get(array, index, threadSafety).AsObject(array);

    private static T? GetObject<T>(FleeceMutableArray array, int index, IThreadSafety? threadSafety = null) where T : class 
        => GetObject(array, index, threadSafety) as T;

    private ThreadSafety SetupThreadSafety()
    {
        Database? db = null;
        if (Array.Context != null && Array.Context != MContext.Null) {
            db = (Array.Context as DocContext)?.Db;
        }

        return db?.ThreadSafety ?? new ThreadSafety();
    }

    /// <inheritdoc />
    public ArrayObject? GetArray(int index) => GetObject<ArrayObject>(Array, index, ThreadSafety);

    /// <inheritdoc />
    public Blob? GetBlob(int index) => GetObject<Blob>(Array, index, ThreadSafety);

    /// <inheritdoc />
    public bool GetBoolean(int index) => DataOps.ConvertToBoolean(GetObject(Array, index, ThreadSafety));

    /// <inheritdoc />
    public DateTimeOffset GetDate(int index) => DataOps.ConvertToDate(GetObject(Array, index, ThreadSafety));

    /// <inheritdoc />
    public DictionaryObject? GetDictionary(int index) => GetObject<DictionaryObject>(Array, index, ThreadSafety);

    /// <inheritdoc />
    public double GetDouble(int index) => DataOps.ConvertToDouble(GetObject(Array, index, ThreadSafety));

    /// <inheritdoc />
    public float GetFloat(int index) => DataOps.ConvertToFloat(GetObject(Array, index, ThreadSafety));

    /// <inheritdoc />
    public int GetInt(int index) => DataOps.ConvertToInt(GetObject(Array, index, ThreadSafety));

    /// <inheritdoc />
    public long GetLong(int index) => DataOps.ConvertToLong(GetObject(Array, index, ThreadSafety));

    /// <inheritdoc />
    public object? GetValue(int index) => GetObject(Array, index, ThreadSafety);

    /// <inheritdoc />
    public string? GetString(int index) => GetObject<string>(Array, index, ThreadSafety);

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public virtual IEnumerator<object?> GetEnumerator() => Array.GetEnumerator();

    /// <inheritdoc />
    public string ToJSON() => Array.IsMutable ? throw new NotSupportedException() : Array.ToJSON();
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class IArrayConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var arr = value as IArray ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError,
            "Invalid input received in WriteJson (not IArray)");
        writer.WriteStartArray();
        foreach (var item in arr) {
            serializer.Serialize(writer, item);
        }

        writer.WriteEndArray();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var arr = new MutableArrayObject();
        while (reader.Read()) {
            switch (reader.TokenType) {
                case JsonToken.EndObject:
                {
                    var val = serializer.Deserialize(reader) as IDictionary<string, object>
                        ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Corrupt input received in ReadJson (EndObject != IDictionary)");
                    if (val.TryGetValue(Constants.ObjectTypeProperty, out var value) &&
                        value.Equals(Constants.ObjectTypeBlob)) {
                        var blob = serializer.Deserialize<Blob>(reader)
                            ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Error deserializing blob in ReadJson (IArray)");
                        arr.AddBlob(blob);
                    } else {
                        var dict = serializer.Deserialize<MutableDictionaryObject>(reader)
                            ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Error deserializing dict in ReadJson (IArray)");
                        arr.AddValue(dict);
                    }
                    break;
                }
                case JsonToken.EndArray:
                {
                    var array = serializer.Deserialize<MutableArrayObject>(reader)
                        ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Error deserializing array in ReadJson (IArray)");
                    arr.AddValue(array);
                    break;
                }
                case JsonToken.None:
                case JsonToken.StartObject:
                case JsonToken.StartArray:
                case JsonToken.StartConstructor:
                case JsonToken.PropertyName:
                case JsonToken.Comment:
                case JsonToken.Raw:
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.String:
                case JsonToken.Boolean:
                case JsonToken.Null:
                case JsonToken.Undefined:
                case JsonToken.EndConstructor:
                case JsonToken.Date:
                case JsonToken.Bytes:
                default:
                    arr.AddValue(reader.Value);
                    break;
            }
        }

        return arr;
    }

    public override bool CanConvert(Type objectType) => typeof(IArray).IsAssignableFrom(objectType);
}
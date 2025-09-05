// 
//  DictionaryObject.cs
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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Support;

namespace Couchbase.Lite;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class IDictionaryObjectConverter : JsonConverter<IDictionaryObject>
{
    public override IDictionaryObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dict = new MutableDictionaryObject();
        if (reader.TokenType == JsonTokenType.StartObject) {
            reader.Read();
        }

        while (reader.TokenType != JsonTokenType.EndObject && reader.Read()) {
            var key = reader.GetString();
            if (key == null) {
                throw new InvalidDataException(CouchbaseLiteErrorMessage.InvalidValueToBeDeserialized);
            }

            reader.Read();
            var value = JsonSerializer.Deserialize<object?>(ref reader, options);
            dict.SetValue(key, value);
        }

        return dict;
    }

    public override void Write(Utf8JsonWriter writer, IDictionaryObject value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var pair in value) {
            writer.WritePropertyName(pair.Key);
            JsonSerializer.Serialize(writer, pair.Value);
        }
        
        writer.WriteEndObject();
    }
}

/// <summary>
/// A class representing a key-value collection that is read only
/// </summary>
public class DictionaryObject : IDictionaryObject, IJSON
{
    internal readonly MDict Dict = new();

    internal readonly ThreadSafety ThreadSafety;
    private List<string>? _keys;

    /// <inheritdoc />
    public IFragment this[string key] => new Fragment(this, key);

    /// <inheritdoc />
    public int Count
    {
        get {
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            return Dict.Count;
        }
    }

    /// <inheritdoc />
    public ICollection<string> Keys
    {
        get {
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            if (_keys != null) {
                return _keys;
            }
            
            var retVal = new List<string>(Dict.Count);
            retVal.AddRange(Dict.AllItems().Select(item => item.Key));
            return _keys = retVal;
        }
    }

    internal DictionaryObject() => ThreadSafety = SetupThreadSafety();

    internal DictionaryObject(MValue mv, MCollection parent)
    {
        Dict.InitInSlot(mv, parent);
        ThreadSafety = SetupThreadSafety();
    }

    internal DictionaryObject(MDict dict, bool isMutable)
    {
        Dict.InitAsCopyOf(dict, isMutable);
        ThreadSafety = SetupThreadSafety();
    }

    /// <summary>
    /// Creates a copy of this object that can be mutated
    /// </summary>
    /// <returns>A mutable copy of the dictionary</returns>
    public MutableDictionaryObject ToMutable()
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        return new MutableDictionaryObject(Dict, true);
    }

    /// <summary>
    /// Signal that the keys of this object have changed (not possible for
    /// this class, but a subclass might)
    /// </summary>
    protected void KeysChanged()
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        _keys = null;
    }

    internal MCollection ToMCollection() => Dict;

    private static object? GetObject(MDict dict, string key, IThreadSafety? threadSafety = null)
    {
        using var threadSafetyScope = threadSafety?.BeginLockedScope();
        return dict.Get(key).AsObject(dict);
    }

    private static T? GetObject<T>(MDict dict, string key, IThreadSafety? threadSafety = null) where T : class 
        => GetObject(dict, key, threadSafety) as T;

    private ThreadSafety SetupThreadSafety()
    {
        Database? db = null;
        if (Dict.Context != null && Dict.Context != MContext.Null) {
            db = (Dict.Context as DocContext)?.Db;
        }

        return db?.ThreadSafety ?? new ThreadSafety();
    }

    /// <inheritdoc />
    public bool Contains(string key)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        return !Dict.Get(key).IsEmpty;
    }

    /// <inheritdoc />
    public ArrayObject? GetArray(string key) => GetObject<ArrayObject>(Dict, key, ThreadSafety);

    /// <inheritdoc />
    public Blob? GetBlob(string key) => GetObject<Blob>(Dict, key, ThreadSafety);

    /// <inheritdoc />
    public bool GetBoolean(string key) => DataOps.ConvertToBoolean(GetObject(Dict, key, ThreadSafety));

    /// <inheritdoc />
    public DateTimeOffset GetDate(string key) => DataOps.ConvertToDate(GetObject(Dict, key, ThreadSafety));

    /// <inheritdoc />
    public DictionaryObject? GetDictionary(string key) => GetObject<DictionaryObject>(Dict, key, ThreadSafety);

    /// <inheritdoc />
    public double GetDouble(string key) => DataOps.ConvertToDouble(GetObject(Dict, key, ThreadSafety));

    /// <inheritdoc />
    public float GetFloat(string key) => DataOps.ConvertToFloat(GetObject(Dict, key, ThreadSafety));

    /// <inheritdoc />
    public int GetInt(string key) => DataOps.ConvertToInt(GetObject(Dict, key, ThreadSafety));

    /// <inheritdoc />
    public long GetLong(string key) => DataOps.ConvertToLong(GetObject(Dict, key, ThreadSafety));

    /// <inheritdoc />
    public object? GetValue(string key) => GetObject(Dict, key, ThreadSafety);

    /// <inheritdoc />
    public string? GetString(string key) => GetObject<string>(Dict, key, ThreadSafety);

    /// <inheritdoc />
    public Dictionary<string, object?> ToDictionary()
    {
        var result = new Dictionary<string, object?>(Dict.Count);
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        foreach (var item in Dict.AllItems()) {
            result[item.Key] = DataOps.ToNetObject(item.Value.AsObject(Dict));
        }

        return result;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public virtual IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => new Enumerator(Dict);

    /// <inheritdoc />
    public string ToJSON() => Dict.IsMutable ? throw new NotSupportedException() : Dict.ToJSON();

    private class Enumerator(MDict parent) : IEnumerator<KeyValuePair<string, object?>>
    {
        private readonly IEnumerator<KeyValuePair<string, MValue>> _inner = parent.AllItems().GetEnumerator();

        object IEnumerator.Current => Current;

        public KeyValuePair<string, object?> Current => new KeyValuePair<string, object?>(_inner.Current.Key,
            _inner.Current.Value.AsObject(parent));

        public void Dispose()
        {
            _inner.Dispose();
        }

        public bool MoveNext() => _inner.MoveNext();

        public void Reset() => _inner.Reset();
    }
}
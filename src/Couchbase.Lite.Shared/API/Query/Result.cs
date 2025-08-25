// 
// QueryResult.cs
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
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Util;

using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite.Query;

/// <summary>
/// A class representing information about a "row" in the result of an
/// <see cref="IQuery"/><br /><br />
/// 
/// > [!WARNING]
/// > The data inside this class is tied to the lifetime of its parent 
/// > <see cref="IResultSet"/> and will become invalid if the parent is 
/// > disposed or garbage collected, unless the data is first converted to
/// > .NET objects via ToList, ToDictionary, etc.
/// </summary>
public sealed unsafe class Result : IArray, IDictionaryObject, IJSON
{
    private const string Tag = nameof(Result);

    private readonly Dictionary<string, int> _columnNames;
    private readonly MContext _context;
    private readonly object?[] _deserialized;
    private readonly BitArray _missingColumns;
    private readonly QueryResultSet _rs;

    private FLArrayIterator _columns;

    /// <summary>
    /// Gets the number of entries in the result
    /// </summary>
    public int Count => _columnNames.Count;

    /// <inheritdoc />
    public IFragment this[int index]
    {
        get {
            _context.CheckDisposed();
            return index >= Count ? Fragment.Null : new Fragment(this, index);
        }
    }

    /// <inheritdoc />
    public IFragment this[string key]
    {
        get {
            _context.CheckDisposed();
            return this[IndexForColumnName(key)];
        }
    }

    /// <inheritdoc />
    public ICollection<string> Keys => _columnNames.Keys;

    internal Result(QueryResultSet rs, C4QueryEnumeratorWrapper e, MContext context)
    {
        _rs = rs;
        _context = context;
        _columns = e.RawEnumerator->columns;
        _missingColumns = new BitArray(BitConverter.GetBytes(e.RawEnumerator->missingColumns));
        _columnNames = new Dictionary<string, int>(_rs.ColumnNames);
        foreach (var pair in _rs.ColumnNames) {
            if (pair.Value < _missingColumns.Length && _missingColumns.Get(pair.Value)) {
                _columnNames.Remove(pair.Key);
            }
        }

        _deserialized = new object[_rs.ColumnNames.Count];
        for (var i = 0; i < _rs.ColumnNames.Count; i++) {
            if (!_missingColumns.Get(i)) {
                _deserialized[i] = FleeceValueToObject(i);
            }
        }
    }

    private object? FleeceValueToObject(int index)
    {
        var value = FLValueAtIndex(index);
        if (value == null) {
            return null;
        }

        var root = new MRoot(_context, value, false);
        return root.AsObject();
    }

    private FLValue* FLValueAtIndex(int index)
    {
        fixed (FLArrayIterator* columns = &_columns) {
            return Native.FLArrayIterator_GetValueAt(columns, (uint) index);
        }
    }

    private int IndexForColumnName(string columnName) => _columnNames.GetValueOrDefault(columnName, -1);

    /// <inheritdoc />
    public ArrayObject? GetArray(int index)
    {
        _context.CheckDisposed();
        return _deserialized[index] as ArrayObject;
    }

    /// <inheritdoc />
    public Blob? GetBlob(int index)
    {
        _context.CheckDisposed();
        return _deserialized[index] as Blob;  
    }

    /// <inheritdoc />
    public bool GetBoolean(int index) => Convert.ToBoolean(_deserialized[index]);

    /// <inheritdoc />
    public DateTimeOffset GetDate(int index) => DataOps.ConvertToDate(_deserialized[index]);

    /// <inheritdoc />
    public DictionaryObject? GetDictionary(int index)
    {
        _context.CheckDisposed();
        return _deserialized[index] as DictionaryObject;
    }

    /// <inheritdoc />
    public double GetDouble(int index) => Convert.ToDouble(_deserialized[index]);

    /// <inheritdoc />
    public float GetFloat(int index) => Convert.ToSingle(_deserialized[index]);

    /// <inheritdoc />
    public int GetInt(int index) => Convert.ToInt32(_deserialized[index]);

    /// <inheritdoc />
    public long GetLong(int index) => Convert.ToInt64(_deserialized[index]);

    /// <inheritdoc />
    public object? GetValue(int index)
    {
        _context.CheckDisposed();
        return _deserialized[index];
    }

    /// <inheritdoc />
    public string? GetString(int index) => _deserialized[index] as string;

    /// <inheritdoc />
    public List<object?> ToList() => _deserialized.Select(DataOps.ToNetObject).ToList();

    /// <inheritdoc />
    public bool Contains(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        return IndexForColumnName(key) >= 0;
    }

    /// <inheritdoc />
    public ArrayObject? GetArray(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 ? GetArray(index) : null;
    }

    /// <inheritdoc />
    public Blob? GetBlob(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 ? GetBlob(index) : null;
    }

    /// <inheritdoc />
    public bool GetBoolean(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 && GetBoolean(index);
    }

    /// <inheritdoc />
    public DateTimeOffset GetDate(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 ? GetDate(index) : DateTimeOffset.MinValue;
    }

    /// <inheritdoc />
    public DictionaryObject? GetDictionary(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 ? GetDictionary(index) : null;
    }

    /// <inheritdoc />
    public double GetDouble(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 ? GetDouble(index) : 0.0;
    }

    /// <inheritdoc />
    public float GetFloat(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 ? GetFloat(index) : 0.0f;
    }

    /// <inheritdoc />
    public int GetInt(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 ? GetInt(index) : 0;
    }

    /// <inheritdoc />
    public long GetLong(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 ? GetLong(index) : 0L;
    }

    /// <inheritdoc />
    public object? GetValue(string key)
    {
        _context.CheckDisposed();
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 ? GetValue(index) : null;
    }

    /// <inheritdoc />
    public string? GetString(string key)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key);
        var index = IndexForColumnName(key);
        return index >= 0 ? GetString(index) : null;
    }

    /// <inheritdoc />
    public Dictionary<string, object?> ToDictionary()
    {
        var dict = new Dictionary<string, object?>();
        foreach (var key in Keys) {
            dict[key] = DataOps.ToNetObject(GetValue(key));
        }

        return dict;
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<object?>) this).GetEnumerator();

    IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
    {
        int index = 0;
        foreach (var column in _rs.ColumnNames.Keys) {
            if (!_missingColumns.Get(index++)) {
                yield return new KeyValuePair<string, object?>(column, GetValue(column));
            }
        }
    }

    IEnumerator<object?> IEnumerable<object?>.GetEnumerator()
    {
        for (var i = 0; i < _rs.ColumnNames.Count; i++) {
            if (!_missingColumns.Get(i)) {
                yield return GetValue(i);
            }
        }
    }

    /// <inheritdoc />
    public string ToJSON() => JsonConvert.SerializeObject(ToDictionary());
}
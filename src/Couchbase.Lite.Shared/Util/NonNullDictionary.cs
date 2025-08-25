// 
// NonNullDictionary.cs
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
using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Lite.Util;

[ExcludeFromCodeCoverage]
internal sealed class CollectionDebuggerView<TKey, TValue>(ICollection<KeyValuePair<TKey, TValue>> col)
{
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public KeyValuePair<TKey, TValue>[] Items
    {
        get {
            var o = new KeyValuePair<TKey, TValue>[col.Count];
            col.CopyTo(o, 0);
            return o;
        }
    }
}

/// <summary>
/// A dictionary that ignores any attempts to insert a null object into it.
/// Usefor for creating JSON objects that should not contain null values
/// </summary>
// ReSharper disable UseNameofExpression
[DebuggerDisplay("Count={Count}")]
// ReSharper restore UseNameofExpression
[DebuggerTypeProxy(typeof(CollectionDebuggerView<,>))]
[ExcludeFromCodeCoverage]
internal sealed class NonNullDictionary<TK, TV> : IDictionary<TK, TV>, IReadOnlyDictionary<TK, TV>
    where TK : notnull
{
    private readonly IDictionary<TK, TV> _data = new Dictionary<TK, TV>();

    /// <inheritdoc cref="ICollection{T}.Count" />
    public int Count => _data.Count;

    /// <inheritdoc />
    public bool IsReadOnly => _data.IsReadOnly;

    /// <inheritdoc cref="IDictionary{TKey,TValue}.this" />
#if NET6_0_OR_GREATER
    [property: NotNull]
#endif
    public TV? this[TK index]
    {
#pragma warning disable CS8766 // Funky because we want setting null to be a no-op, but returning to not allow null
        get => _data[index]!;
#pragma warning restore CS8766
        set {
            if(IsAddable(value)) {
                _data[index] = value!;
            }
        }
    }

    /// <inheritdoc />
    public ICollection<TK> Keys => _data.Keys;

    /// <inheritdoc />
    public ICollection<TV> Values => _data.Values;

    /// <inheritdoc />
    IEnumerable<TK> IReadOnlyDictionary<TK, TV>.Keys => _data.Keys;

    /// <inheritdoc />
    IEnumerable<TV> IReadOnlyDictionary<TK, TV>.Values => _data.Values;

    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse", Justification = "item is a Nullable type during the block")]
    private static bool IsAddable(TV? item)
    {
        if (item is not ValueType) {
            return item != null;
        }

        var underlyingType = Nullable.GetUnderlyingType(typeof(TV));
        if(underlyingType != null) {
            return item != null;
        }

        return true;
    }

    /// <inheritdoc />
    public void Add(KeyValuePair<TK, TV> item)
    {
        if(IsAddable(item.Value)) {
            _data.Add(item);
        }
    }

    /// <inheritdoc />
    public void Clear() => _data.Clear();

    /// <inheritdoc />
    public bool Contains(KeyValuePair<TK, TV> item) => _data.Contains(item);

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex) => _data.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public bool Remove(KeyValuePair<TK, TV> item) => _data.Remove(item);

    /// <inheritdoc />
    public void Add(TK key, TV value)
    {
        if(IsAddable(value)) {
            _data.Add(key, value);
        }
    }

    /// <inheritdoc cref="IDictionary{TKey,TValue}.ContainsKey" />
    public bool ContainsKey(TK key) => _data.ContainsKey(key);

    /// <inheritdoc />
    public bool Remove(TK key) => _data.Remove(key);

    /// <inheritdoc cref="IDictionary{TKey,TValue}.TryGetValue" />
    public bool TryGetValue(TK key,
#if NET6_0_OR_GREATER
        [MaybeNullWhen(false)]
#endif
        out TV value) => _data.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator() => _data.GetEnumerator();
}
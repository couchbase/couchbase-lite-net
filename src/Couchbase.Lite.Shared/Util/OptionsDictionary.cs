// 
// OptionsDictionary.cs
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

namespace Couchbase.Lite;

/// <summary>
/// An abstract base class for options dictionaries.  These dictionaries are simply
/// dictionaries of <see cref="String"/> and <see cref="Object"/> but they provide 
/// safe accessors to get the data without having to know the keys they are stored
/// under
/// </summary>
internal abstract class OptionsDictionary : IDictionary<string, object?>
{
    private readonly Dictionary<string, object?> _inner = new Dictionary<string, object?>();

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public int Count => _inner.Count;

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public object? this[string key]
    {
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        get => _inner[key];
        set {
            if (!Validate(key, value)) {
                throw new InvalidOperationException($"Invalid value {value} for key '{key}'");
            }

            _inner[key] = value;
        }
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public ICollection<string> Keys => _inner.Keys;

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public ICollection<object?> Values => _inner.Values;

    internal OptionsDictionary()
    {

    }

    internal OptionsDictionary(Dictionary<string, object?> raw) => _inner = raw;

    internal void Build() => BuildInternal();

    internal virtual void BuildInternal()
    { }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal virtual bool KeyIsRequired(string key) => false;

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal virtual bool Validate(string key, object? value) => true;

    /// <inheritdoc />
    public void Add(KeyValuePair<string, object?> item)
    {
        if (!Validate(item.Key, item.Value)) {
            throw new InvalidOperationException($"Invalid value {item.Value} for key '{item.Key}'");
        }

        ((ICollection<KeyValuePair<string, object?>>)_inner).Add(item);
    }

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public bool Contains(KeyValuePair<string, object?> item) => 
        ((ICollection<KeyValuePair<string, object?>>)_inner).Contains(item);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) => 
        ((ICollection<KeyValuePair<string, object?>>)_inner).CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public bool Remove(KeyValuePair<string, object?> item) => 
        KeyIsRequired(item.Key) 
            ? throw new InvalidOperationException($"Cannot remove the required key '{item.Key}'") 
            : ((ICollection<KeyValuePair<string, object?>>)_inner).Remove(item);

    /// <inheritdoc />
    public void Add(string key, object? value)
    {
        if (!Validate(key, value)) {
            throw new InvalidOperationException($"Invalid value {value} for key '{key}'");
        }

        _inner.Add(key, value);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public bool ContainsKey(string key) => _inner.ContainsKey(key);

    /// <inheritdoc />
    public bool Remove(string key) => 
        KeyIsRequired(key) 
            ? throw new InvalidOperationException($"Cannot remove the required key '{key}'") 
            : _inner.Remove(key);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public bool TryGetValue(string key, out object? value) => _inner.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _inner.GetEnumerator();
}
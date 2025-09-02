// 
// ArrayIndexConfiguration.cs
// 
// Copyright (c) 2021 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Internal.Query;
using LiteCore.Interop;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Couchbase.Lite.Query;

/// <summary>
/// Configuration for indexing property values within nested arrays
/// in documents, intended for use with the UNNEST query keyword.
/// </summary>
public sealed record ArrayIndexConfiguration : IndexConfiguration
{
    /// <summary>
    /// Path to the array, which can be nested.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public required string Path { get; init; }
    
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="path">Path to the array, which can be nested to be indexed. 
    /// Use "[]" to represent a property that is an array of each
    /// nested array level.For a single array or the last level 
    /// array, the "[]" is optional.  For instance, use 
    /// "contacts[].phones" to specify an array of phones within each 
    /// contact.
    /// </param>
    /// <param name="expressions">An optional collection of strings, where each string 
    /// represents an expression defining the values within the array
    /// to be indexed.If the array specified by the path contains
    /// scalar values.
    /// </param>
    [SetsRequiredMembers]
    public ArrayIndexConfiguration(string path, IEnumerable<string>? expressions = null) 
        : base(C4IndexType.ArrayIndex, expressions?.ToArray() ?? [])
    {
        Path = path;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="path">Path to the array, which can be nested to be indexed. 
    /// Use "[]" to represent a property that is an array of each
    /// nested array level.For a single array or the last level 
    /// array, the "[]" is optional.  For instance, use 
    /// "contacts[].phones" to specify an array of phones within each 
    /// contact.
    /// </param>
    /// <param name="expressions">An optional collection of strings, where each string 
    /// represents an expression defining the values within the array
    /// to be indexed.If the array specified by the path contains
    /// scalar values.
    /// </param>
    [SetsRequiredMembers]
    public ArrayIndexConfiguration(string path, params string[] expressions)
        : base(C4IndexType.ArrayIndex, expressions)
    {
        Path = path;
    }

    internal override C4IndexOptions Options => new()
    {
        unnestPath = Path
    };
}
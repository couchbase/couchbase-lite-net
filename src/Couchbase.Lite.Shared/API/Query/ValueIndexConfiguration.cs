// 
// ValueIndexConfiguration.cs
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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Couchbase.Lite.Internal.Query;
using LiteCore.Interop;

namespace Couchbase.Lite.Query;

/// <summary>
/// A class for an index based on a simple property value
/// </summary>
public sealed record ValueIndexConfiguration : IndexConfiguration
{
    internal override C4IndexOptions Options => new()
    {
        where = Where
    };
    
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public string? Where { get; }
    
    /// <summary>
    /// Starts the creation of an index based on a simple property
    /// </summary>
    /// <param name="expressions">The expressions to use to create the index</param>
    /// <returns>The beginning of a value based index</returns>
    public ValueIndexConfiguration(params string[] expressions)
        : base(C4IndexType.ValueIndex, expressions)
    {
    }
    
    /// <summary>
    /// Starts the creation of an index based on one or more simple property values,
    /// and a predicate for enabling partial indexes.
    /// </summary>
    /// <param name="expressions">The expressions to use to create the index</param>
    /// <param name="where">A where clause used to determine whether to include a particular doc</param>
    public ValueIndexConfiguration(IEnumerable<string> expressions, string? where = null)
        : base(C4IndexType.ValueIndex, expressions.ToArray()) =>
        Where = where;
}
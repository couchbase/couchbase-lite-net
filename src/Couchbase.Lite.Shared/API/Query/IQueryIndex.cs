// 
//  IQueryIndex.cs
// 
//  Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

using LiteCore.Interop;
using System;

namespace Couchbase.Lite.Query;

/// <summary>
/// An interface representing an existing index in a collection
/// </summary>
public partial interface IQueryIndex : IDisposable
{
    /// <summary>
    /// The collection that this index belongs to
    /// </summary>
    Collection Collection { get; }

    /// <summary>
    /// The name of the index
    /// </summary>
    string Name { get; }
}

internal sealed partial class QueryIndexImpl(C4IndexWrapper index, Collection collection, string name) 
    : IQueryIndex
{
    public Collection Collection { get; } = collection;

    public string Name { get; } = name;

    public void Dispose()
    {
        index.Dispose();
    }
}
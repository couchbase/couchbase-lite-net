// 
// IIndexable.cs
// 
// Copyright (c) 2022 Couchbase, Inc All rights reserved.
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
using System;
using System.Collections.Generic;

namespace Couchbase.Lite.Query;

/// <summary>
/// An interface describing an object That can create, delete, or retrieve a list of existing
/// <see cref="IIndex"/> objects by name
/// </summary>
public interface IIndexable
{
    /// <summary>
    /// Gets a list of index names that are present in the database
    /// </summary>
    /// <returns>The list of created index names</returns>
    IList<string> GetIndexes();

    /// <summary>
    /// Creates a SQL index which could be a value index from <see cref="ValueIndexConfiguration"/> or a full-text search index
    /// from <see cref="FullTextIndexConfiguration"/> with the given name.
    /// The name can be used for deleting the index. Creating a new different index with an existing
    /// index name will replace the old index; creating the same index with the same name will be no-ops.
    /// </summary>
    /// <param name="name">The index name</param>
    /// <param name="indexConfig">The index</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> or <paramref name="indexConfig"/>
    /// is <c>null</c></exception>
    /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
    /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
    /// <exception cref="NotSupportedException">Thrown if an implementation of <see cref="IIndex"/> other than one of the library
    /// provided ones is used</exception>
    void CreateIndex(string name, IndexConfiguration indexConfig);

    /// <summary>
    /// Deletes the index with the given name
    /// </summary>
    /// <param name="name">The name of the index to delete</param>
    void DeleteIndex(string name);
}
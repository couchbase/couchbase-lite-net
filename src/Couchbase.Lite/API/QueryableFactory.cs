//
//  QueryableFactory.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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

using System.Linq;

using Couchbase.Lite.DB;
using Couchbase.Lite.Querying;

namespace Couchbase.Lite
{
    /// <summary>
    /// A factory for creating <see cref="IQueryable{T}"/> instances based on a given database 
    /// </summary>
    public static class QueryableFactory
    {
        /// <summary>
        /// Creates an <see cref="IQueryable{T}"/> instance based on the given database
        /// </summary>
        /// <typeparam name="TElement">The type of element to return from the query</typeparam>
        /// <param name="db">The database to operate on</param>
        /// <returns>The instantiated <see cref="IQueryable{T}"/> instance</returns>
        public static IQueryable<T> MakeQueryable<T>(IDatabase db) where T : class, IDocumentModel, new()
        {
            return new DatabaseQueryable<T>(db as Database);
        }

        internal static IQueryable<string> MakeDebugQueryable()
        {
            return new DatabaseDebugQueryable();
        }
    }
}

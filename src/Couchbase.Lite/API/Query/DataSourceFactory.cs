// 
// DataSourceFactory.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
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
using Couchbase.Lite.Internal.Query;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A factory class for creating data sources for queries
    /// </summary>
    public static class DataSourceFactory
    {
        #region Public Methods

        /// <summary>
        /// Creates a data source for an <see cref="IQuery" /> that gets results from the given
        /// <see cref="Database" />
        /// </summary>
        /// <param name="database">The database to operate on</param>
        /// <returns>The source of data for the <see cref="IQuery" /></returns>
        public static IDatabaseSource Database(Database database)
        {
            return new DatabaseSource(database);
        }

        #endregion

        #region Internal Methods

        //internal static IQueryable<TElement> LinqDataSource<TElement>(Database database, bool prefetch)
        //    where TElement : class, IDocumentModel, new()
        //{
        //    if (database == null) {
        //        throw new ArgumentNullException(nameof(database));
        //    }

        //    var db = database as Database;
        //    if (db == null) {
        //        throw new NotSupportedException("Custom IDatabase not supported");
        //    }

        //    return new DatabaseQueryable<TElement>(db, prefetch);
        //}

        #endregion
    }
}

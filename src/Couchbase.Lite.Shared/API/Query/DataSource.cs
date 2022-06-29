﻿// 
//  DataSource.cs
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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Util;

using JetBrains.Annotations;
using System;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A factory class for creating data sources for queries
    /// </summary>
    public static class DataSource
    {
        #region Constants

        private const string Tag = nameof(DataSource);

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a data source for an <see cref="IQuery" /> that gets results from the given
        /// <see cref="Collection(Lite.Collection)" />
        /// </summary>
        /// <param name="collection">The collection to operate on</param>
        /// <returns>The source of data for the <see cref="IQuery" /></returns>
        [NotNull]
        public static IDataSourceAs Collection([NotNull] Collection collection)
        {
            var c = CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(collection), collection);
            return new DatabaseSource(c, c.ThreadSafety);
        }

        /// <summary>
        /// [DEPRECATED] Creates a data source for an <see cref="IQuery" /> that gets results from the given
        /// <see cref="Database" />
        /// </summary>
        /// <param name="database">The database to operate on</param>
        /// <returns>The source of data for the <see cref="IQuery" /></returns>
        [Obsolete("DataSource.Database is deprecated, please use DataSource.Collection")]
        [NotNull]
        public static IDataSourceAs Database([NotNull]Database database)
        {
            var db = CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(database), database);

            return new DatabaseSource(database, db.ThreadSafety);
        }

        #endregion
    }
}

﻿// 
// Join.cs
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
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Util;
using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A class for creating <see cref="IJoin"/> instances
    /// </summary>
    public static class Join
    {
        #region Constants

        private const string Tag = nameof(Join);

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a CROSS JOIN clause
        /// </summary>
        /// <param name="dataSource">The data source to JOIN with</param>
        /// <returns>An <see cref="IJoinOn"/> instance for processing</returns>
        [NotNull]
        public static IJoin CrossJoin([NotNull]IDataSource dataSource)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(dataSource), dataSource);
            return new QueryJoin("CROSS", dataSource);
        }

        /// <summary>
        /// Creates an INNER JOIN clause
        /// </summary>
        /// <param name="dataSource">The data source to JOIN with</param>
        /// <returns>An <see cref="IJoinOn"/> instance for processing</returns>
        [NotNull]
        public static IJoinOn InnerJoin([NotNull]IDataSource dataSource)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(dataSource), dataSource);
            return new QueryJoin(null, dataSource);
        }

        /// <summary>
        /// Synonym for <see cref="LeftOuterJoin(IDataSource)"/>
        /// </summary>
        /// <param name="dataSource">The data source to JOIN with</param>
        /// <returns>An <see cref="IJoinOn"/> instance for processing</returns>
        [NotNull]
        public static IJoinOn LeftJoin([NotNull]IDataSource dataSource)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(dataSource), dataSource);
            return LeftOuterJoin(dataSource);
        }

        /// <summary>
        /// Creates a LEFT OUTER JOIN clause
        /// </summary>
        /// <param name="dataSource">The data source to JOIN with</param>
        /// <returns>An <see cref="IJoinOn"/> instance for processing</returns>
        [NotNull]
        public static IJoinOn LeftOuterJoin([NotNull]IDataSource dataSource)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(dataSource), dataSource);
            return new QueryJoin("LEFT OUTER", dataSource);
        }

        #endregion
    }
}

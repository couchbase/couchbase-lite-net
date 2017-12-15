// 
// Join.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A class for creating <see cref="IJoin"/> instances
    /// </summary>
    public static class Join
    {
        #region Public Methods

        /// <summary>
        /// Creates a CROSS JOIN clause
        /// </summary>
        /// <param name="dataSource">The data source to JOIN with</param>
        /// <returns>An <see cref="IJoinOn"/> instance for processing</returns>
        [NotNull]
        public static IJoinOn CrossJoin(IDataSource dataSource)
        {
            return new QueryJoin("CROSS", dataSource);
        }

        /// <summary>
        /// Creates an INNER JOIN clause
        /// </summary>
        /// <param name="dataSource">The data source to JOIN with</param>
        /// <returns>An <see cref="IJoinOn"/> instance for processing</returns>
        [NotNull]
        public static IJoinOn InnerJoin(IDataSource dataSource)
        {
            return new QueryJoin(null, dataSource);
        }

        /// <summary>
        /// Synonym for <see cref="InnerJoin(IDataSource)"/>
        /// </summary>
        /// <param name="dataSource">The data source to JOIN with</param>
        /// <returns>An <see cref="IJoinOn"/> instance for processing</returns>
        [NotNull]
        public static IJoinOn DefaultJoin(IDataSource dataSource)
        {
            return InnerJoin(dataSource);
        }

        /// <summary>
        /// Synonym for <see cref="LeftOuterJoin(IDataSource)"/>
        /// </summary>
        /// <param name="dataSource">The data source to JOIN with</param>
        /// <returns>An <see cref="IJoinOn"/> instance for processing</returns>
        [NotNull]
        public static IJoinOn LeftJoin(IDataSource dataSource)
        {
            return LeftOuterJoin(dataSource);
        }

        /// <summary>
        /// Creates a LEFT OUTER JOIN clause
        /// </summary>
        /// <param name="dataSource">The data source to JOIN with</param>
        /// <returns>An <see cref="IJoinOn"/> instance for processing</returns>
        [NotNull]
        public static IJoinOn LeftOuterJoin(IDataSource dataSource)
        {
            return new QueryJoin("LEFT OUTER", dataSource);
        }

        #endregion
    }
}

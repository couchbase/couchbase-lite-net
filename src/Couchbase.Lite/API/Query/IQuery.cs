// 
// IQuery.cs
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
using System;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An interface representing a runnable query over a data source
    /// </summary>
    public interface IQuery : IDisposable
    {
        #region Properties

        /// <summary>
        /// Gets the parameter collection for this query so that parameters may be
        /// added for substitution into the query API (via <see cref="Expression.Parameter(int)"/>
        /// or <see cref="Expression.Parameter(string)"/>)
        /// </summary>
        IParameters Parameters { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Runs the query
        /// </summary>
        /// <returns>The results of running the query</returns>
        IResultSet Execute();

        /// <summary>
        /// Converts a query to a <see cref="ILiveQuery"/> for realtime monitoring.
        /// </summary>
        /// <returns>The instantiated live query object</returns>
        ILiveQuery ToLive();

        #endregion
    }
}

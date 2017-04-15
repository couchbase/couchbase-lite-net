// 
// ILiveQuery.cs
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
using System.Collections.Generic;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An interface for a query which reports any changes in its results in
    /// real time
    /// </summary>
    internal interface ILiveQuery : IDisposable
    {
        #region Variables

        /// <summary>
        /// An event that fires when the query's result set has changed
        /// </summary>
        event EventHandler<LiveQueryChangedEventArgs> Changed;

        #endregion

        #region Properties

        /// <summary>
        /// The last retrieved results from this query
        /// </summary>
        IEnumerable<IQueryRow> Results { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the monitoring process for the live query (to stop, 
        /// the live query must be disposed)
        /// </summary>
        void Start();

        #endregion
    }
}

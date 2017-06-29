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
    /// Arguments for the <see cref="ILiveQuery.Changed" /> event
    /// </summary>
    public sealed class LiveQueryChangedEventArgs : EventArgs
    {
        #region Properties

        /// <summary>
        /// Gets the updated rows of the query
        /// </summary>
        public IResultSet Rows { get; }

        /// <summary>
        /// Gets the error that occurred, if any
        /// </summary>
        public Exception Error { get; }

        #endregion

        #region Constructors

        internal LiveQueryChangedEventArgs(IResultSet rows, Exception e = null)
        {
            Rows = rows;
            Error = e;
        }

        #endregion
    }

    /// <summary>
    /// An interface for a query which reports any changes in its rows in
    /// real time.
    /// </summary>
    public interface ILiveQuery : IDisposable
    {
        #region Variables

        /// <summary>
        /// An event that fires when the query's result set has changed
        /// </summary>
        event EventHandler<LiveQueryChangedEventArgs> Changed;

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the monitoring process for the live query (to stop, 
        /// the live query must be disposed).
        /// </summary>
        void Run();

        /// <summary>
        /// Stops observing the database for changes.
        /// </summary>
        void Stop();

        #endregion
    }
}

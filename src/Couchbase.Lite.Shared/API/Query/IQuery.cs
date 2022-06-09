// 
// IQuery.cs
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
using System.Threading.Tasks;

using Couchbase.Lite.Internal.Query;

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// Arguments for the <see cref="IQuery.AddChangeListener(TaskScheduler, EventHandler{QueryChangedEventArgs})" /> event
    /// </summary>
    public sealed class QueryChangedEventArgs : EventArgs
    {
        #region Properties

        /// <summary>
        /// Gets the error that occurred, if any
        /// </summary>
        [CanBeNull]
        public Exception Error { get; }

        /// <summary>
        /// Gets the updated rows of the query
        /// </summary>
        [NotNull]
        [ItemNotNull]
        public IResultSet Results { get; }

        #endregion

        #region Constructors

        internal QueryChangedEventArgs(IResultSet rows, Exception e = null)
        {
            Results = rows ?? new NullResultSet();
            Error = e;
        }

        #endregion
    }

    /// <summary>
    /// An interface representing a runnable query over a data source
    /// </summary>
    public interface IQuery : IChangeObservable<QueryChangedEventArgs>, IDisposable
    {
        #region Properties

        /// <summary>
        /// Gets or sets the parameter collection for this query so that parameters may be
        /// added for substitution into the query API (via <see cref="Expression.Parameter(string)"/>)
        /// </summary>
        /// <remarks>
        /// The returned collection is a copy, and must be reset onto the query instance.
        /// Doing so will trigger a re-run and update any listeners.
        /// </remarks>
        [NotNull]
        Parameters Parameters { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Runs the query
        /// </summary>
        /// <returns>The results of running the query</returns>
        /// <exception cref="InvalidOperationException">Thrown if this query has
        /// no database to operate on, or if it is missing SELECT or FROM statements (unusual)</exception>
        [NotNull]
        IResultSet Execute();

        /// <summary>
        /// Gets an explanation of what the query will do
        /// </summary>
        /// <returns>The explanation of the query</returns>
        /// <exception cref="ObjectDisposedException">Thrown if this method is
        /// called after disposal</exception>
        [NotNull]
        string Explain();

        #endregion
    }
}

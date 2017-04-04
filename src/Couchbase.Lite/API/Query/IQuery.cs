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
using System.Collections.Generic;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An interface representing a runnable query over a data source
    /// </summary>
    public interface IQuery : IDisposable
    {

        #region Public Methods

        /// <summary>
        /// Runs the query
        /// </summary>
        /// <returns>The results of running the query</returns>
        IEnumerable<IQueryRow> Run();

        /// <summary>
        /// Skips the first given number of items in the query
        /// </summary>
        /// <param name="skip">The number of items to skip before returning data</param>
        /// <returns>The query object for further processing</returns>
        IQuery Skip(ulong skip);

        /// <summary>
        /// Converts a query to a <see cref="ILiveQuery"/> for realtime monitoring.
        /// </summary>
        /// <returns>The instantiated live query object</returns>
        ILiveQuery ToLiveQuery();

        /// <summary>
        /// Sets the limit for the number of items to return from the query
        /// </summary>
        /// <param name="limit">The maximum number of items to return from the query</param>
        /// <returns></returns>
        IQuery Limit(ulong limit);

        //IQuery SetParameters(IDictionary<string, object> parameters);

        #endregion
    }
}

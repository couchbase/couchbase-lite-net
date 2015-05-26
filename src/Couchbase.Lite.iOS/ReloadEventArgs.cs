//
//  CouchBaseTableDelegate.cs
//
//  Author:
//      Unknown (Current maintainer: Jim Borden  <jim.borden@couchbase.com>)
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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

using System;

namespace Couchbase.Lite.iOS
{

    /// <summary>
    /// Event arguments for a table view reload event
    /// </summary>
    public class ReloadEventArgs : EventArgs
    {

        /// <summary>
        /// Gets the query being used to drive the source that has been reloaded
        /// </summary>
        public Query Query { get; private set; }

        /// <summary>
        /// Gets the result rows of the query
        /// </summary>
        public QueryEnumerator Rows { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="query">The query being used to drive the source that has been reloaded</param>
        /// <param name="rows">The result rows, or null</param>
        public ReloadEventArgs(Query query, QueryEnumerator rows = null)
        {
            Query = query;
            Rows = rows;
        }
    }

}


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

using Foundation;
using UIKit;

namespace Couchbase.Lite.iOS
{
    /// <summary>
    /// This class provides a way to conveniently adapt a Couchbase Lite query
    /// for use in an iOS UITableView interaction
    /// </summary>
    public abstract class CouchBaseTableDelegate : UITableViewDelegate
    {

        /// <summary>
        /// Creates a cell based on a certain source
        /// </summary>
        /// <returns>The row for use in the UITableView</returns>
        /// <param name="source">The adapted data source for use with UITableView</param>
        /// <param name="indexPath">The index path for the cell requested</param>
        public abstract UITableViewCell CellForRowAtIndexPath (CouchbaseTableSource source, NSIndexPath indexPath);

        /// <summary>
        /// Called before the source updates from the given query
        /// </summary>
        /// <param name="source">The source that will be updated</param>
        /// <param name="query">The query that will modify the source</param>
        public abstract void WillUpdateFromQuery (CouchbaseTableSource source, LiveQuery query);

        /// <summary>
        /// Called when the source is updated by the given query
        /// </summary>
        /// <param name="source">The source being updated</param>
        /// <param name="query">The query updating the source</param>
        /// <param name="previousRows">The rows as they existed before the update</param>
        public abstract void UpdateFromQuery (CouchbaseTableSource source, LiveQuery query, QueryRow [] previousRows);

        /// <summary>
        /// Called before a cell is used for display.  Gives the chance for some
        /// initialization and/or customization
        /// </summary>
        /// <param name="source">The source being used to create the cell</param>
        /// <param name="cell">The cell about to be used</param>
        /// <param name="row">The row with the data for the cell</param>
        public abstract void WillUseCell (CouchbaseTableSource source, UITableViewCell cell, QueryRow row);

        /// <summary>
        /// Attempts to delete the given row from the query results
        /// </summary>
        /// <returns><c>true</c>, if row was deleted, <c>false</c> otherwise.</returns>
        /// <param name="source">The source containing all the data for the table</param>
        /// <param name="row">The row to attempt deletion on</param>
        public abstract bool DeleteRow (CouchbaseTableSource source, QueryRow row);

        /// <summary>
        /// Called when a call to <c>DeleteRow</c> fails
        /// </summary>
        /// <param name="source">The source which was passed to <c>DeleteRow</c></param>
        public abstract void DeleteFailed (CouchbaseTableSource source);
    }
}

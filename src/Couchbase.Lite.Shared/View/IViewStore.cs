//
//  IViewStore.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
using System.Collections.Generic;

namespace Couchbase.Lite.Store
{

    /// <summary>
    /// Storage for a view. Instances are created by ICouchStore implementations, and are owned by
    /// View instances.
    /// </summary>
    internal interface IViewStore
    {

        /// <summary>
        /// The name of the view.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The delegate (in practice, the owning View itself.)
        /// </summary>
        /// <value>The delegate.</value>
        IViewStoreDelegate Delegate { get; set; }

        /// <summary>
        /// Closes the storage.
        /// </summary>
        void Close();

        /// <summary>
        /// Erases the view's index.
        /// </summary>
        void DeleteIndex();

        /// <summary>
        /// Deletes the view's storage (metadata and index), removing it from the database.
        /// </summary>
        void DeleteView();

        /// <summary>
        /// Updates the version of the view. A change in version means the delegate's map block has
        /// changed its semantics, so the index should be deleted.
        /// </summary>
        bool SetVersion(string version);

        /// <summary>
        /// The total number of rows in the index.
        /// </summary>
        int TotalRows { get; }

        /// <summary>
        /// The last sequence number that has been indexed.
        /// </summary>
        long LastSequenceIndexed { get; }

        /// <summary>
        /// The last sequence number that caused an actual change in the index.
        /// </summary>
        long LastSequenceChangedAt { get; }

        /// <summary>
        /// Updates the indexes of one or more views in parallel.
        /// </summary>
        /// <returns>The success/error status.</returns>
        /// <param name="views">An array of IViewStorage instances, always including the receiver.</param>
        Status UpdateIndexes(IEnumerable<IViewStore> views);

        /// <summary>
        /// Queries the view without performing any reducing or grouping.
        /// </summary>
        IEnumerable<QueryRow> RegularQuery(QueryOptions options);

        /// <summary>
        /// Queries the view, with reducing or grouping as per the options.
        /// </summary>
        IEnumerable<QueryRow> ReducedQuery(QueryOptions options);

        /*TODO: Full text
        /// <summary>
        /// Performs a full-text query as per the options.
        /// </summary>
        QueryEnumerator FullTextQuery(QueryOptions options);*/

        /// <summary>
        /// Gets the backing store for the specified query row
        /// </summary>
        /// <returns>The backing store for the specified query row</returns>
        /// <param name="row">The specified query row</param>
        IQueryRowStore StorageForQueryRow(QueryRow row);

        /// <summary>
        /// Create a JSON string representing the info of this view (for testing purposes)
        /// </summary>
        IEnumerable<IDictionary<string, object>> Dump();

    }
}



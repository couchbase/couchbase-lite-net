//
// QueryOptions.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    /// <summary>
    /// Standard query options for views.
    /// </summary>
    public class QueryOptions
    {

        #region Constants

        internal const int DEFAULT_LIMIT = int.MaxValue;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the start key for the query
        /// </summary>
        public object StartKey { get; set; }

        /// <summary>
        /// Gets or sets the end key for the query.
        /// </summary>
        public object EndKey { get; set; }

        /// <summary>
        /// Gets or sets the keys to include in the query
        /// </summary>
        public IEnumerable<object> Keys { get; set; }

        /// <summary>
        /// Gets or sets the number of documents the query should skip
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// Gets or sets the number of results the query is limited to
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Gets or sets the group level of the query
        /// </summary>
        /// <value>The group level.</value>
        public int GroupLevel { get; set; }

        /// <summary>
        /// Gets or sets the content options for including document values in the results
        /// </summary>
        public DocumentContentOptions ContentOptions { get; set; }

        /// <summary>
        /// Gets or sets whether or not the query is in descending order
        /// </summary>
        public bool Descending { get; set; }

        /// <summary>
        /// Gets or sets whether or not to include document information in the results
        /// </summary>
        public bool IncludeDocs { get; set; }

        /// <summary>
        /// Gets or sets whether or not to include deleted documents in the result set
        /// </summary>
        public bool IncludeDeletedDocs { get; set; }

        /// <summary>
        /// Gets or sets whether or not to include the most recently updated sequence number
        /// from the database in the result set
        /// </summary>
        public bool UpdateSeq { get; set; }

        /// <summary>
        /// Gets or sets whether or not to include the start key in the result set
        /// </summary>
        public bool InclusiveStart { get; set; }

        /// <summary>
        /// Gets or sets whether or not to include the end key in the result set
        /// </summary>
        public bool InclusiveEnd { get; set; }

        /// <summary>
        /// Gets or sets whether or not this query should reduce
        /// </summary>
        public bool Reduce { get; set; }

        /// <summary>
        /// Gets or sets whether or not the reduce parameter was explicitly specified
        /// (Defaults vary depending on whether or not it was)
        /// </summary>
        public bool ReduceSpecified { get; set; }

        /// <summary>
        /// Gets or sets whether or not this query groups its results
        /// </summary>
        public bool Group { get; set; }

        /// <summary>
        /// Gets or sets the timing for updating the results of the query
        /// </summary>
        public IndexUpdateMode Stale { get; set; }

        /// <summary>
        /// Gets or sets the options for an _all_docs query
        /// </summary>
        /// <value>All documents mode.</value>
        public AllDocsMode AllDocsMode { get; set; }

        /// <summary>
        /// Gets or sets the first document ID to include in the results
        /// </summary>
        public string StartKeyDocId { get; set; }

        /// <summary>
        /// Gets or sets the last document ID to include in the results
        /// </summary>
        public string EndKeyDocId { get; set; }

        /// <summary>
        /// If nonzero, enables prefix matching of string or array keys.
        ///* A value of 1 treats the endKey itself as a prefix: if it's a string, keys in the index that
        ///    come after the endKey, but begin with the same prefix, will be matched. (For example, if the
        ///        endKey is "foo" then the key "foolish" in the index will be matched, but not "fong".) Or if
        ///    the endKey is an array, any array beginning with those elements will be matched. (For
        ///        example, if the endKey is [1], then [1, "x"] will match, but not [2].) If the key is any
        ///    other type, there is no effect.
        ///    * A value of 2 assumes the endKey is an array and treats its final item as a prefix, using the
        ///        rules above. (For example, an endKey of [1, "x"] will match [1, "xtc"] but not [1, "y"].)
        ///        * A value of 3 assumes the key is an array of arrays, etc.
        ///        Note that if the .Descending property is also set, the search order is reversed and the above
        ///            discussion applies to the startKey, _not_ the endKey.
        /// </summary>
        public int PrefixMatchLevel { get; set; }

        /// <summary>
        /// Gets or sets the filter used for filtering the results of the query
        /// </summary>
        /// <value>The filter.</value>
        public Func<QueryRow, bool> Filter { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        public QueryOptions()
        {
            Limit = DEFAULT_LIMIT;
            InclusiveEnd = true;
            InclusiveStart = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the start key for the query
        /// </summary>
        [Obsolete("Use the appropriate property")]
        public object GetStartKey()
        {
            return StartKey;
        }

        /// <summary>
        /// Sets the start key for the query
        /// </summary>
        /// <param name="startKey">The start key</param>
        [Obsolete("Use the appropriate property")]
        public void SetStartKey(object startKey)
        {
            StartKey = startKey;
        }

        /// <summary>
        /// Gets the end key for the query
        /// </summary>
        /// <returns>The end key.</returns>
        [Obsolete("Use the appropriate property")]
        public object GetEndKey()
        {
            return EndKey;
        }

        /// <summary>
        /// Sets the end key for the query
        /// </summary>
        /// <param name="endKey">The end key</param>
        [Obsolete("Use the appropriate property")]
        public void SetEndKey(object endKey)
        {
            EndKey = endKey;
        }

        /// <summary>
        /// Gets the number of docs to skip in the query
        /// </summary>
        /// <returns>The number of docs to skip</returns>
        [Obsolete("Use the appropriate property")]
        public int GetSkip()
        {
            return Skip;
        }

        /// <summary>
        /// Sets the number of docs to skip in the query
        /// </summary>
        /// <param name="skip">The number of docs to skip</param>
        [Obsolete("Use the appropriate property")]
        public void SetSkip(int skip)
        {
            Skip = skip;
        }

        /// <summary>
        /// Gets the number of docs to limit the query to
        /// </summary>
        /// <returns>The doc count limit</returns>
        [Obsolete("Use the appropriate property")]
        public int GetLimit()
        {
            return Limit;
        }

        /// <summary>
        /// Sets the number of docs to limit the query to
        /// </summary>
        /// <param name="limit">The doc count limit</param>
        [Obsolete("Use the appropriate property")]
        public void SetLimit(int limit)
        {
            Limit = limit;
        }

        /// <summary>
        /// Returns whether or not the query should order the results in descending in order
        /// </summary>
        /// <returns><c>true</c> if this instance is in descending order; 
        /// otherwise, <c>false</c>.</returns>
        [Obsolete("Use the appropriate property")]
        public bool IsDescending()
        {
            return Descending;
        }

        /// <summary>
        /// Sets whether or not the query should order the results in descending in order
        /// </summary>
        /// <param name="descending">If set to <c>true</c> order descending.</param>
        [Obsolete("Use the appropriate property")]
        public void SetDescending(bool descending)
        {
            Descending = descending;
        }

        /// <summary>
        /// Returns whether or not the document bodies should be included with the query results
        /// </summary>
        /// <returns><c>true</c> if this instance includes document bodies; otherwise, <c>false</c>.</returns>
        [Obsolete("Use the appropriate property")]
        public bool IsIncludeDocs()
        {
            return IncludeDocs;
        }

        /// <summary>
        /// Sets whether or not the document bodies should be included with the query results
        /// </summary>
        /// <param name="includeDocs">If set to <c>true</c> include document bodies.</param>
        [Obsolete("Use the appropriate property")]
        public void SetIncludeDocs(bool includeDocs)
        {
            IncludeDocs = includeDocs;
        }

        /// <summary>
        /// Get the all document query mode for this query
        /// </summary>
        /// <returns>The all document query mode.</returns>
        /// <remarks>
        /// Naturally, this only applies to _all_docs queries
        /// </remarks>
        [Obsolete("Use the appropriate property")]
        public AllDocsMode GetAllDocsMode()
        {
            return AllDocsMode;
        }

        /// <summary>
        /// Sets the all document query mode for this query
        /// </summary>
        /// <param name="allDocsMode">The all document query mode.</param>
        /// <remarks>
        /// Naturally, this only applies to _all_docs queries
        /// </remarks>
        [Obsolete("Use the appropriate property")]
        public void SetAllDocsMode(AllDocsMode allDocsMode)
        {
            AllDocsMode = allDocsMode;
        }

        /// <summary>
        /// Gets whether or not the results include the last sequence updated in the database
        /// </summary>
        /// <returns><c>true</c> if this instance returns the update sequence; otherwise, <c>false</c>.</returns>
        [Obsolete("Use the appropriate property")]
        public bool IsUpdateSeq()
        {
            return UpdateSeq;
        }

        /// <summary>
        /// Sets whether or not the results include the last sequence updated in the database
        /// </summary>
        /// <param name="updateSeq"><c>true</c> if this instance returns the update sequence; 
        /// otherwise, <c>false</c>.</param>
        [Obsolete("Use the appropriate property")]
        public void SetUpdateSeq(bool updateSeq)
        {
            UpdateSeq = updateSeq;
        }

        /// <summary>
        /// Gets whether or not the query includes the end key
        /// </summary>
        /// <returns><c>true</c> if this instance includes the end key; otherwise, <c>false</c>.</returns>
        [Obsolete("Use the appropriate property")]
        public bool IsInclusiveEnd()
        {
            return InclusiveEnd;
        }

        /// <summary>
        /// Sets whether or not the query includes the end key
        /// </summary>
        /// <param name="inclusiveEnd"><c>true</c> if this instance includes the end key; otherwise, <c>false</c>.</param>
        [Obsolete("Use the appropriate property")]
        public void SetInclusiveEnd(bool inclusiveEnd)
        {
            InclusiveEnd = inclusiveEnd;
        }

        /// <summary>
        /// Gets the group level of the query
        /// </summary>
        /// <returns>The group level.</returns>
        [Obsolete("Use the appropriate property")]
        public int GetGroupLevel()
        {
            return GroupLevel;
        }

        /// <summary>
        /// Sets the group level of the query
        /// </summary>
        /// <param name="groupLevel">Group level.</param>
        [Obsolete("Use the appropriate property")]
        public void SetGroupLevel(int groupLevel)
        {
            GroupLevel = groupLevel;
        }

        /// <summary>
        /// Gets whether or not this query should reduce
        /// </summary>
        /// <returns><c>true</c> if this instance should reduce; otherwise, <c>false</c>.</returns>
        [Obsolete("Use the appropriate property")]
        public bool IsReduce()
        {
            return Reduce;
        }

        /// <summary>
        /// Sets whether or not this query should reduce
        /// </summary>
        /// <param name="reduce">If set to <c>true</c> reduce.</param>
        [Obsolete("Use the appropriate property")]
        public void SetReduce(bool reduce)
        {
            Reduce = reduce;
        }

        /// <summary>
        /// Gets whether or not this query groups its results
        /// </summary>
        /// <returns><c>true</c> if this instance groups its results; otherwise, <c>false</c>.</returns>
        [Obsolete("Use the appropriate property")]
        public bool IsGroup()
        {
            return Group;
        }

        /// <summary>
        /// Sets whether or not this query groups its results
        /// </summary>
        /// <param name="group">If set to <c>true</c> group.</param>
        [Obsolete("Use the appropriate property")]
        public void SetGroup(bool group)
        {
            Group = group;
        }

        /// <summary>
        /// Get the options for including document content in the result set
        /// </summary>
        /// <returns>The content options.</returns>
        [Obsolete("Use the appropriate property")]
        public DocumentContentOptions GetContentOptions()
        {
            return ContentOptions;
        }

        /// <summary>
        /// Sets the options for including document content in the result set
        /// </summary>
        /// <param name="contentOptions">Content options.</param>
        [Obsolete("Use the appropriate property")]
        public void SetContentOptions(DocumentContentOptions contentOptions )
        {
            ContentOptions = contentOptions;
        }

        /// <summary>
        /// Gets the keys to include in the query results
        /// </summary>
        /// <returns>The keys to include in the query results.</returns>
        [Obsolete("Use the appropriate property")]
        public IEnumerable<object> GetKeys()
        {
            return Keys;
        }

        /// <summary>
        /// Sets the keys to include in the query results
        /// </summary>
        /// <param name="keys">The keys to include in the query results.</param>
        [Obsolete("Use the appropriate property")]
        public void SetKeys(IEnumerable<object> keys)
        {
            Keys = keys;
        }

        /// <summary>
        /// Gets the timing of when to update the query results
        /// </summary>
        /// <returns>The timing of when to update the query results</returns>
        [Obsolete("Use the appropriate property")]
        public IndexUpdateMode GetStale()
        {
            return Stale;
        }

        /// <summary>
        /// Sets the timing of when to update the query results
        /// </summary>
        /// <param name="stale">The timing of when to update the query results</param>
        [Obsolete("Use the appropriate property")]
        public void SetStale(IndexUpdateMode stale)
        {
            Stale = stale;
        }

        /// <summary>
        /// Gets whether or not to include deleted documents in the reuslt set
        /// </summary>
        /// <returns><c>true</c> if this instance includes deleted documents; otherwise, <c>false</c>.</returns>
        [Obsolete("Use the appropriate property")]
        public bool IsIncludeDeletedDocs()
        {
            return IncludeDeletedDocs;
        }

        /// <summary>
        /// Sets whether or not to include deleted documents in the reuslt set
        /// </summary>
        /// <param name="includeDeletedDocs">If set to <c>true</c> include deleted documents.</param>
        [Obsolete("Use the appropriate property")]
        public void SetIncludeDeletedDocs(bool includeDeletedDocs)
        {
            IncludeDeletedDocs = includeDeletedDocs;
        }

        /// <summary>
        /// Gets whether or not the Reduce property has been manually specified
        /// </summary>
        /// <returns><c>true</c> if this instance is manually specified; otherwise, <c>false</c>.</returns>
        [Obsolete("Use the appropriate property")]
        public bool IsReduceSpecified()
        {
            return ReduceSpecified;
        }

        /// <summary>
        /// Sets whether or not the Reduce property has been manually specified
        /// </summary>
        /// <param name="reduceSpecified">If set to <c>true</c> reduce is specified.</param>
        [Obsolete("Use the appropriate property")]
        public void SetReduceSpecified(bool reduceSpecified)
        {
            ReduceSpecified = reduceSpecified;
        }

        /// <summary>
        /// Gets the first document ID to include in the result set
        /// </summary>
        /// <returns>The first document ID to include in the result set</returns>
        [Obsolete("Use the appropriate property")]
        public string GetStartKeyDocId()
        {
            return StartKeyDocId;
        }

        /// <summary>
        /// Sets the first document ID to include in the result set
        /// </summary>
        /// <param name="startKeyDocId">The first document ID to include in the result set</param>
        [Obsolete("Use the appropriate property")]
        public void SetStartKeyDocId(string startKeyDocId)
        {
            StartKeyDocId = startKeyDocId;
        }

        /// <summary>
        /// Gets the last document ID to include in the result set
        /// </summary>
        /// <returns>The last document ID to include in the result set</returns>
        [Obsolete("Use the appropriate property")]
        public string GetEndKeyDocId()
        {
            return EndKeyDocId;
        }

        /// <summary>
        /// Sets the last document ID to include in the result set
        /// </summary>
        /// <param name="endKeyDocId">The last document ID to include in the result set</param>
        [Obsolete("Use the appropriate property")]
        public void SetEndKeyDocId(string endKeyDocId)
        {
            EndKeyDocId = endKeyDocId;
        }

        #endregion
    }
}

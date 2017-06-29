// 
// IQueryRow.cs
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

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface describing an entry of a result set for
    /// a plain value query
    /// </summary>
    public interface IQueryRow
    {
        #region Properties

        /// <summary>
        /// Gets the document that was used for creating this
        /// entry
        /// </summary>
        Document Document { get; }

        /// <summary>
        /// Gets the ID of the document that was used for 
        /// creating this entry
        /// </summary>
        string DocumentID { get; }

        /// <summary>
        /// Gets the sequence of the document that was used for 
        /// creating this entry
        /// </summary>
        ulong Sequence { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the <see cref="Boolean"/> value of the nth selected
        /// value of the query row (in order of what was specified in the
        /// SELECT portion of the query)
        /// </summary>
        /// <param name="index">The index of the element to retrieve in terms
        /// of the SELECT query</param>
        /// <returns>The value at the index</returns>
        bool GetBoolean(int index);

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> value of the nth selected
        /// value of the query row (in order of what was specified in the
        /// SELECT portion of the query)
        /// </summary>
        /// <param name="index">The index of the element to retrieve in terms
        /// of the SELECT query</param>
        /// <returns>The value at the index</returns>
        DateTimeOffset GetDate(int index);

        /// <summary>
        /// Gets the <see cref="Double"/> value of the nth selected
        /// value of the query row (in order of what was specified in the
        /// SELECT portion of the query)
        /// </summary>
        /// <param name="index">The index of the element to retrieve in terms
        /// of the SELECT query</param>
        /// <returns>The value at the index</returns>
        double GetDouble(int index);

        /// <summary>
        /// Gets the <see cref="Int32"/> value of the nth selected
        /// value of the query row (in order of what was specified in the
        /// SELECT portion of the query)
        /// </summary>
        /// <param name="index">The index of the element to retrieve in terms
        /// of the SELECT query</param>
        /// <returns>The value at the index</returns>
        int GetInt(int index);

        /// <summary>
        /// Gets the <see cref="Int64"/> value of the nth selected
        /// value of the query row (in order of what was specified in the
        /// SELECT portion of the query)
        /// </summary>
        /// <param name="index">The index of the element to retrieve in terms
        /// of the SELECT query</param>
        /// <returns>The value at the index</returns>
        long GetLong(int index);

        /// <summary>
        /// Gets the value of the nth selected
        /// value of the query row (in order of what was specified in the
        /// SELECT portion of the query)
        /// </summary>
        /// <param name="index">The index of the element to retrieve in terms
        /// of the SELECT query</param>
        /// <returns>The value at the index</returns>
        object GetObject(int index);

        /// <summary>
        /// Gets the <see cref="String"/> value of the nth selected
        /// value of the query row (in order of what was specified in the
        /// SELECT portion of the query)
        /// </summary>
        /// <param name="index">The index of the element to retrieve in terms
        /// of the SELECT query</param>
        /// <returns>The value at the index</returns>
        string GetString(int index);

        #endregion
    }
}

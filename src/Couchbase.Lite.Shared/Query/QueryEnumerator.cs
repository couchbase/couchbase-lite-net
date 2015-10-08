//
// QueryEnumerator.cs
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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {
    
    /// <summary>
    /// An enumerator for Couchbase Lite <see cref="Couchbase.Lite.View"/> <see cref="Couchbase.Lite.Query"/> results.
    /// </summary>
    public sealed class QueryEnumerator : IEnumerator<QueryRow>, IEnumerable<QueryRow>
    {
        private readonly int _count;
    #region Constructors

        internal QueryEnumerator (QueryEnumerator rows)
        {
            Database = rows.Database;
            Rows = rows.Rows;
            _count = rows.Count;
            SequenceNumber = rows.SequenceNumber;

            Reset();
        }

        internal QueryEnumerator (Database database, IEnumerable<QueryRow> rows, Int64 lastSequence)
        {
            Database = database;
            Rows = rows;
            _count = rows.Count();
            SequenceNumber = lastSequence;

            Reset();
        }

    #endregion

    #region Non-public Members
    
        private Database Database { get; set; }

        private IEnumerable<QueryRow> Rows { get; set; }

        private Int32 CurrentRow { get; set; }

    #endregion

    #region Instance Members
        //Properties

        /// <summary>
        /// Gets the number of rows in the <see cref="Couchbase.Lite.QueryEnumerator"/>.
        /// </summary>
        /// <value>The number of rows in the <see cref="Couchbase.Lite.QueryEnumerator"/>.</value>
        public Int32 Count { get { return _count; } }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/>'s sequence number at the time the View results were generated.
        /// </summary>
        /// <value>The sequence number.</value>
        public Int64 SequenceNumber { get; private set; }

        /// <summary>
        /// Gets whether the <see cref="Couchbase.Lite.Database"/> has changed since 
        /// the <see cref="Couchbase.Lite.View"/> results were generated.
        /// </summary>
        /// <value><c>true</c> if stale; otherwise, <c>false</c>.</value>
        public Boolean Stale { get { return SequenceNumber < Database.LastSequenceNumber; } }


        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.QueryRow"/> at the specified index in the results.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.QueryRow"/> at the specified index in the results.</returns>
        /// <param name="index">Index.</param>
        public QueryRow GetRow(Int32 index) {
            var row = Rows.ElementAt(index);
            row.Database = Database; // Avoid multiple enumerations by doing this here instead of the constructor.
            return row;
        }

    #endregion

    #region Operator Overloads

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="Couchbase.Lite.QueryEnumerator"/>.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="Couchbase.Lite.QueryEnumerator"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current
        /// <see cref="Couchbase.Lite.QueryEnumerator"/>; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            return GetHashCode() == obj.GetHashCode();
        }

        /// <summary>
        /// Serves as a hash function for a <see cref="Couchbase.Lite.QueryEnumerator"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a
        /// hash table.</returns>
        public override int GetHashCode ()
        {
            var idString = String.Format("{0}{1}{2}{3}", Database.Path, Count, SequenceNumber, Stale);
            return idString.GetHashCode ();
        }


    #endregion

    #region IEnumerator Implementation

        /// <Docs>The collection was modified after the enumerator was instantiated.</Docs>
        /// <attribution license="cc4" from="Microsoft" modified="false"></attribution>
        /// <see cref="M:System.Collections.IEnumerator.MoveNext"></see>
        /// <see cref="M:System.Collections.IEnumerator.Reset"></see>
        /// <see cref="T:System.InvalidOperationException"></see>
        /// <summary>
        /// Resets the <see cref="Couchbase.Lite.QueryEnumerator"/>'s cursor position 
        /// so that the next call to next() will return the first row.
        /// </summary>
        public void Reset() {
            CurrentRow = -1; 
            Current = null;
        }

        /// <summary>
        /// Gets the current <see cref="Couchbase.Lite.QueryRow"/> from the results.
        /// </summary>
        /// <value>The current QueryRow.</value>
        public QueryRow Current { get; private set; }

        /// <summary>
        /// Gets the next <see cref="Couchbase.Lite.QueryRow"/> from the results.
        /// </summary>
        /// <returns><c>true</c>, if next was moved, <c>false</c> otherwise.</returns>
        public Boolean MoveNext ()
        {
            if (++CurrentRow >= Count)
                return false;

            Current = GetRow(CurrentRow);

            return true;
        }

        /// <summary>
        /// Releases all resource used by the <see cref="Couchbase.Lite.QueryEnumerator"/> object.
        /// </summary>
        /// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Couchbase.Lite.QueryEnumerator"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="Couchbase.Lite.QueryEnumerator"/> in an unusable state.
        /// After calling <see cref="Dispose"/>, you must release all references to the
        /// <see cref="Couchbase.Lite.QueryEnumerator"/> so the garbage collector can reclaim the memory that the
        /// <see cref="Couchbase.Lite.QueryEnumerator"/> was occupying.</remarks>
        public void Dispose ()
        {
            Database = null;
            Rows = null;
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.QueryRow"/> from the results.
        /// </summary>
        /// <value>The current QueryRow.</value>
        Object IEnumerator.Current { get { return Current; } }

    #endregion

    #region IEnumerable implementation

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<QueryRow> GetEnumerator ()
        {
            return new QueryEnumerator(this);
        }

        #endregion

        #region IEnumerable implementation
        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator ()
        {
            return new QueryEnumerator(this);
        }

    #endregion

    }

    

}

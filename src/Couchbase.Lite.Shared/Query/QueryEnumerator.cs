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
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite {
    
    /// <summary>
    /// An enumerator for Couchbase Lite <see cref="Couchbase.Lite.View"/> <see cref="Couchbase.Lite.Query"/> results.
    /// </summary>
    public sealed class QueryEnumerator : IEnumerator<QueryRow>, IEnumerable<QueryRow>
    {
        
        #region Variables

        private readonly int _count;

        #endregion

        #region Properties

        private Database Database { get; set; }

        private IEnumerable<QueryRow> Rows { get; set; }

        private int CurrentRow { get; set; }

        /// <summary>
        /// Gets the number of rows in the <see cref="Couchbase.Lite.QueryEnumerator"/>.
        /// </summary>
        public int Count { get { return _count; } }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/>'s sequence number at the time the View results were generated.
        /// </summary>
        public long SequenceNumber { get; private set; }

        /// <summary>
        /// Gets whether the <see cref="Couchbase.Lite.Database"/> has changed since 
        /// the <see cref="Couchbase.Lite.View"/> results were generated.
        /// </summary>
        public bool Stale { get { return SequenceNumber < Database.LastSequenceNumber; } }

        #endregion

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

        #region Public Methods

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.QueryRow"/> at the specified index in the results.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.QueryRow"/> at the specified index in the results.</returns>
        /// <param name="index">Index.</param>
        public QueryRow GetRow(int index) {
            var row = Rows.ElementAt(index);
            row.Database = Database; // Avoid multiple enumerations by doing this here instead of the constructor.
            return row;
        }

        #endregion

        #region Overloads
        #pragma warning disable 1591

        public override bool Equals(object obj)
        {
            if (this == obj) {
                return true;
            }

            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            return GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode ()
        {
            var idString = String.Format("{0}{1}{2}{3}", Database.DbDirectory, Count, SequenceNumber, Stale);
            return idString.GetHashCode ();
        }


        #endregion

        #region IEnumerator

        public void Reset() {
            CurrentRow = -1; 
            Current = null;
        }

        public QueryRow Current { get; private set; }

        public bool MoveNext ()
        {
            if (++CurrentRow >= Count)
                return false;

            Current = GetRow(CurrentRow);

            return true;
        }

        public void Dispose ()
        {
            Database = null;
            Rows = null;
        }

        object System.Collections.IEnumerator.Current { get { return Current; } }

        #endregion

        #region IEnumerable

        public IEnumerator<QueryRow> GetEnumerator ()
        {
            return new QueryEnumerator(this);
        }
            
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
        {
            return new QueryEnumerator(this);
        }

        #pragma warning restore 1591
        #endregion

    }
}

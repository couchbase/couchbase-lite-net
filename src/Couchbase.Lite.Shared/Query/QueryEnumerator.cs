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
using System.Diagnostics;

namespace Couchbase.Lite {
    
    /// <summary>
    /// An enumerator for Couchbase Lite <see cref="Couchbase.Lite.View"/> <see cref="Couchbase.Lite.Query"/> results.
    /// </summary>
    public sealed class QueryEnumerator : IEnumerator<QueryRow>, IEnumerable<QueryRow>
    {
        
        #region Variables

        private int _count = -1;
        private readonly IEnumerable<QueryRow> _rows;
        private readonly IEnumerator<QueryRow> _enumerator;
        private readonly Database _database;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of rows in the <see cref="Couchbase.Lite.QueryEnumerator"/>.
        /// </summary>
        public int Count
        {
            get {
                if (_count == -1) {
                    _count = _rows.Count();
                }

                return _count;
            }
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/>'s sequence number at the time the View results were generated.
        /// </summary>
        public long SequenceNumber { get; private set; }

        /// <summary>
        /// Gets whether the <see cref="Couchbase.Lite.Database"/> has changed since 
        /// the <see cref="Couchbase.Lite.View"/> results were generated.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Obsolete("This property is heavy and will be replaced by IsStale()")]
        public bool Stale
        { 
            get { 
                return IsStale();
            } 
        }

        #endregion

        #region Constructors

        internal QueryEnumerator (QueryEnumerator rows)
        {
            _database = rows._database;
            _rows = rows._rows;
            _count = rows._count;
            _enumerator = _rows.GetEnumerator();
            SequenceNumber = rows.SequenceNumber;
        }

        internal QueryEnumerator (Database database, IEnumerable<QueryRow> rows, long lastSequence)
        {
            _database = database;
            _rows = rows;
            _count = rows.Count();
            _enumerator = rows.GetEnumerator();
            SequenceNumber = lastSequence;
        }

        #endregion

        #region Public Methods

        public bool IsStale()
        {
            return SequenceNumber < _database.GetLastSequenceNumber(); 
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.QueryRow"/> at the specified index in the results.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.QueryRow"/> at the specified index in the results.</returns>
        /// <param name="index">Index.</param>
        [Obsolete("Use LINQ ElementAt")]
        public QueryRow GetRow(int index) {
            var row = _rows.ElementAt(index);
            row.Database = _database; // Avoid multiple enumerations by doing this here instead of the constructor.
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
            var idString = String.Format("{0}{1}{2}{3}", _database.DbDirectory, Count, SequenceNumber, IsStale());
            return idString.GetHashCode ();
        }


        #endregion

        #region IEnumerator

        public void Reset() {
            _enumerator?.Reset();
        }

        public QueryRow Current 
        {
            get {
                var retVal = _enumerator?.Current;
                if(retVal == null) {
                    return null;
                }

                retVal.Database = _database;
                return retVal;
            }
        }

        public bool MoveNext ()
        {
            if(_enumerator == null) {
                return false;
            }

            return _enumerator.MoveNext();
        }

        public void Dispose ()
        {
            _enumerator?.Dispose();
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

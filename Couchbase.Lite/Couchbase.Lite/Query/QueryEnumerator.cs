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
    
    public partial class QueryEnumerator : IEnumerator<QueryRow>, IEnumerable<QueryRow>
    {

    #region Constructors

        internal QueryEnumerator (QueryEnumerator rows)
        {
            Database = rows.Database;
            Rows = rows.Rows;
            SequenceNumber = rows.SequenceNumber;
        }

        internal QueryEnumerator (Database database, IEnumerable<QueryRow> rows, Int64 lastSequence)
        {
            Database = database;
            Rows = rows;
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
        public Int32 Count { get { return Rows.Count(); } }

        public Int64 SequenceNumber { get; private set; }

        /// <summary>True if the database has changed since the view was generated.</summary>
        public Boolean Stale { get { return SequenceNumber < Database.GetLastSequenceNumber(); } }

        /// <summary>
        /// Advances to the next QueryRow in the results, or false
        /// if there are no more results.
        /// </summary>
        public QueryRow GetRow(Int32 index) {
            var row = Rows.ElementAt(index);
            row.Database = Database; // Avoid multiple enumerations by doing this here instead of the constructor.
            return row;
        }

    #endregion

    #region Operator Overloads

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

        public override int GetHashCode ()
        {
            var idString = String.Format("{0}{1}{2}{3}", Database.Path, Count, SequenceNumber, Stale);
            return idString.GetHashCode ();
        }


    #endregion

    #region IEnumerator Implementation

        public void Reset() {
            CurrentRow = -1; 
            Current = null;
        }

        public QueryRow Current { get; private set; }

        public Boolean MoveNext ()
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

        Object IEnumerator.Current { get { return Current; } }

    #endregion

    #region IEnumerable implementation

        public IEnumerator<QueryRow> GetEnumerator ()
        {
            return this;
        }

        #endregion

        #region IEnumerable implementation

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return this;
        }

    #endregion

    }

    

}

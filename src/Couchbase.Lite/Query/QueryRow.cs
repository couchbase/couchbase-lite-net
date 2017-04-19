//
//  QueryRow.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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

using System.Diagnostics;
using Couchbase.Lite.DB;
using LiteCore.Interop;

namespace Couchbase.Lite.Querying
{
    internal unsafe class QueryRow : IQueryRow
    {
        #region Variables

        protected readonly Database _db;

        #endregion

        #region Properties

        public IDocument Document
        {
            get {
                var retVal = _db.GetDocument(DocumentID);
                Debug.Assert(retVal != null);
                return retVal;
            }
        }

        public string DocumentID { get; }

        public ulong Sequence { get; set; }

        #endregion

        #region Constructors

        internal QueryRow(Database db, C4QueryEnumerator* enumerator)
        {
            _db = db;
            DocumentID = enumerator->docID.CreateString();
            Debug.Assert(DocumentID != null);
            Sequence = enumerator->docSequence;
        }

        #endregion
    }
}
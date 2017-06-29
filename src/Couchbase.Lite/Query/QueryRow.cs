// 
// QueryRow.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Logging;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query
{
    internal unsafe class QueryRow : IQueryRow
    {
        #region Variables

        protected FLArrayIterator _columns;
        protected bool _current;

        protected QueryEnumerator _enum;

        #endregion

        #region Properties

        public int Count => (int)Native.c4query_columnCount(_enum.C4Query);

        public Document Document => DocumentID != null ? _enum.Database.GetDocument(DocumentID) : null;

        public string DocumentID { get; }

        public object this[int index] => GetObject(index);

        public ulong Sequence { get; set; }

        #endregion

        #region Constructors

        internal QueryRow(QueryEnumerator enumerator, C4QueryEnumerator* e)
        {
            _enum = enumerator;
            DocumentID = e->docID.CreateString();
            Sequence = e->docSequence;
            _columns = e->columns;
            _current = true;
        }

        #endregion

        #region Public Methods

        public void StopBeingCurrent()
        {
            _current = false;
        }

        #endregion

        #region Private Methods

        private FLValue* FLValueAtIndex(int index)
        {
            if (!_current) {
                throw new InvalidOperationException(
                    "You cannot access an IQueryRow value after the enumerator has advanced past that row");
            }

            fixed (FLArrayIterator* columns = &_columns) {
                return Native.FLArrayIterator_GetValueAt(columns, (uint)index);
            }
        }

        #endregion

        #region IQueryRow

        public bool GetBoolean(int index)
        {
            return Native.FLValue_AsBool(FLValueAtIndex(index));
        }

        public DateTimeOffset GetDate(int index)
        {
            return DataOps.ConvertToDate(GetObject(index));
        }

        public double GetDouble(int index)
        {
            return Native.FLValue_AsDouble(FLValueAtIndex(index));
        }

        public int GetInt(int index)
        {
            return (int)Native.FLValue_AsInt(FLValueAtIndex(index));
        }

        public long GetLong(int index)
        {
            return Native.FLValue_AsInt(FLValueAtIndex(index));
        }

        public object GetObject(int index)
        {
            return FLValueConverter.ToObject(FLValueAtIndex(index), null);
        }

        public string GetString(int index)
        {
            return Native.FLValue_AsString(FLValueAtIndex(index));
        }

        #endregion
    }
}
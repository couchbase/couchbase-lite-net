// 
// QueryResultSet.cs
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
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed unsafe class QueryResultSet : IResultSet
    {
        #region Constants

        private const string Tag = nameof(QueryResultSet);

        #endregion

        #region Variables

        private readonly C4QueryEnumerator* _c4Enum;

        private readonly XQuery _query;

        #endregion

        #region Properties

        public int Count { get; }

        internal IDictionary<string, int> ColumnNames { get; }

        internal Database Database => _query?.Database;

        #endregion

        #region Constructors

        internal QueryResultSet(XQuery query, C4QueryEnumerator* e, IDictionary<string, int> columnNames)
        {
            _query = query;
            _c4Enum = e;
            Count = (int)Native.c4queryenum_getRowCount(e, null);
            ColumnNames = columnNames;
            Log.To.Query.I(Tag, $"Beginning query enumeration ({(long) _c4Enum:x})");
        }

        ~QueryResultSet()
        {
            Dispose(false);
        }

        #endregion

        #region Public Methods

        public QueryResultSet Refresh()
        {
            var query = _query;
            if (query == null) {
                return null;
            }

            var newEnum = (C4QueryEnumerator*)LiteCoreBridge.Check(err => Native.c4queryenum_refresh(_c4Enum, err));
            return newEnum != null ? new QueryResultSet(query, newEnum, ColumnNames) : null;
        }

        #endregion

        #region Private Methods

        private void Dispose(bool disposing)
        {
            Native.c4queryenum_free(_c4Enum);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<IResult>

        public IEnumerator<IResult> GetEnumerator()
        {
            return new Enumerator(this);
        }

        #endregion

        #region Nested

        private class Enumerator : IEnumerator<IResult>
        {
            #region Variables

            private readonly QueryResultSet _parent;

            #endregion

            #region Properties

            public IResult Current => new QueryResult(_parent, _parent._c4Enum);

            object IEnumerator.Current => Current;

            #endregion

            #region Constructors

            public Enumerator(QueryResultSet parent)
            {
                _parent = parent;
            }

            #endregion

            #region IDisposable

            public void Dispose()
            {
                Native.c4queryenum_seek(_parent._c4Enum, 0, null);
            }

            #endregion

            #region IEnumerator

            public bool MoveNext()
            {
                C4Error err;
                var moved = Native.c4queryenum_next(_parent._c4Enum, &err);
                if (!moved && err.code != 0) {
                    throw new LiteCoreException(err);
                }

                return moved;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            #endregion
        }

        #endregion
    }
}
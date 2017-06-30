// 
// QueryEnumerator.cs
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
using System.Diagnostics.CodeAnalysis;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed unsafe class QueryEnumerator : IResultSet
    {
        #region Constants

        private const string Tag = nameof(QueryEnumerator);

        #endregion

        #region Variables

        private readonly C4QueryEnumerator* _c4Enum;

        private readonly IQueryInternal _query;

        #endregion

        #region Properties

        public int Count { get; }

        internal C4Query* C4Query { get; }

        internal Database Database => _query?.Database;

        #endregion

        #region Constructors

        public QueryEnumerator(IQueryInternal query, C4Query* c4Query, C4QueryEnumerator* e)
        {
            Debug.Assert(e != null);
            _query = query;
            C4Query = c4Query;
            _c4Enum = e;
            Count = (int)Native.c4queryenum_getRowCount(_c4Enum, null);
            Log.To.Query.I(Tag, $"Beginning query enumeration (0x{(long)_c4Enum:x}");
        }

        ~QueryEnumerator()
        {
            Dispose(false);
        }

        #endregion

        #region Public Methods

        public QueryEnumerator Refresh()
        {
            var query = _query;
            if (query == null) {
                return null;
            }

            var newEnum = (C4QueryEnumerator *)LiteCoreBridge.Check(err => Native.c4queryenum_refresh(_c4Enum, err));
            return new QueryEnumerator(query, C4Query, newEnum);
        }

        #endregion

        #region Private Methods

        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
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

        #region IEnumerable<IQueryRow>

        public IEnumerator<IQueryRow> GetEnumerator()
        {
            return new Enumerator(this);
        }

        #endregion

        #region Nested

        private class Enumerator : IEnumerator<IQueryRow>
        {
            #region Variables

            private readonly QueryEnumerator _parent;

            #endregion

            #region Properties

            public IQueryRow Current => _parent._c4Enum->fullTextTermCount > 0
                ? new FullTextQueryRow(_parent, _parent._c4Enum)
                : new QueryRow(_parent, _parent._c4Enum);

            object IEnumerator.Current => Current;

            #endregion

            #region Constructors

            public Enumerator(QueryEnumerator parent)
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
                ((QueryRow)Current)?.StopBeingCurrent();
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
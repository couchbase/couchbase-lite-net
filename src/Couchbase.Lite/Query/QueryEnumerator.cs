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
using Couchbase.Lite.Logging;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed unsafe class QueryEnumerator : IEnumerator<IQueryRow>, IReadOnlyList<IQueryRow>
    {
        private const string Tag = nameof(QueryEnumerator);

        #region Variables

        private readonly IQueryInternal _query;
        private readonly C4QueryEnumerator* _c4Enum;
        private bool _randomAccess;

        #endregion

        #region Properties

        public int Count { get; }

        public IQueryRow Current
        {
            get {
                if (_c4Enum->docID.buf == null) {
                    return null;
                }

                return _c4Enum->fullTextTermCount > 0
                    ? new FullTextQueryRow(this, _c4Enum)
                    : new QueryRow(this, _c4Enum);
            }
        }

        public IQueryRow this[int index]
        {
            get {
                _randomAccess = true;
                LiteCoreBridge.Check(err => Native.c4queryenum_seek(_c4Enum, (ulong) index, err));
                return Current;
            }
        }

        object IEnumerator.Current => Current;

        internal C4Query* C4Query { get; }

        internal Database Database => _query?.Database;

        #endregion

        public QueryEnumerator(IQueryInternal query, C4Query* c4Query, C4QueryEnumerator* e)
        {
            if (e == null) {
                throw new ArgumentNullException(nameof(e));
            }

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

        public QueryEnumerator Refresh()
        {
            var query = _query;
            if (query == null) {
                return null;
            }

            var newEnum = (C4QueryEnumerator *)LiteCoreBridge.Check(err => Native.c4queryenum_refresh(_c4Enum, err));
            return new QueryEnumerator(query, C4Query, newEnum);
        }

        private void Dispose(bool disposing)
        {
            Native.c4queryenum_free(_c4Enum);
        }

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

        #region IEnumerable<object>

        public IEnumerator<IQueryRow> GetEnumerator()
        {
            return this;
        }

        #endregion

        #region IEnumerator

        public bool MoveNext()
        {
            if (_randomAccess) {
                throw new InvalidOperationException("Cannot enumerate in random access mode");
            }

            ((QueryRow)Current)?.StopBeingCurrent();
            C4Error err;
            var moved = Native.c4queryenum_next(_c4Enum, &err);
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
}
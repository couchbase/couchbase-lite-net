// 
//  QueryResultSet.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;

using JetBrains.Annotations;

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
        [NotNull]private readonly QueryResultContext _context;
        private readonly XQuery _query;
        [NotNull]private readonly ThreadSafety _threadSafety;
        private bool _disposed;
        private bool _randomAccess;

        #endregion

        #region Properties

        internal IDictionary<string, int> ColumnNames { get; }

        internal Database Database => _query?.Database;

        internal QueryResult this[int index]
        {
            get {
                _threadSafety.DoLockedBridge(err =>
                {
                    if (_disposed) {
                        throw new ObjectDisposedException(nameof(QueryResultSet));
                    }

                    return Native.c4queryenum_seek(_c4Enum, (ulong) index, err);
                });

                return new QueryResult(this, _c4Enum, _context);
            }
        }

        public int Count { get; }

        #endregion

        #region Constructors

        internal QueryResultSet(XQuery query, [NotNull]ThreadSafety threadSafety, C4QueryEnumerator* e,
            IDictionary<string, int> columnNames)
        {
            _query = query;
            _c4Enum = e;
            var count = 0;
            threadSafety.DoLocked(() =>
            {
                count = (int) Native.c4queryenum_getRowCount(e, null);
            });
            Count = count;
            ColumnNames = columnNames;
            _threadSafety = threadSafety;
            _context = new QueryResultContext(query.Database, e);
        }

        #endregion

        #region Public Methods

        public QueryResultSet Refresh()
        {
            var query = _query;
            if (query == null) {
                return null;
            }

            var newEnum = (C4QueryEnumerator*)_threadSafety.DoLockedBridge(err =>
            {
                if (_disposed) {
                    return null;
                }

                return Native.c4queryenum_refresh(_c4Enum, err);
            });

            return newEnum != null ? new QueryResultSet(query, _threadSafety, newEnum, ColumnNames) : null;
        }

        public IReadOnlyList<QueryResult> ToArray()
        {
            IReadOnlyList<QueryResult> retVal = null;
            _threadSafety.DoLocked(() =>
            {
                if (_disposed) {
                    throw new ObjectDisposedException(nameof(QueryResultSet));
                }

                if (Count >= 0) {
                    _randomAccess = true;
                    retVal = new QueryResultsArray(this, Count);
                    return;
                }

                retVal = Enumerable.Empty<QueryResult>().ToArray();
            });

            return retVal;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _threadSafety.DoLocked(() =>
            {
                _disposed = true;
                _context?.Dispose();
            });
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<QueryResult>

        public IEnumerator<QueryResult> GetEnumerator()
        {
            _threadSafety.DoLocked(() =>
            {
                if (_disposed) {
                    throw new ObjectDisposedException(nameof(QueryResultSet));
                }
            });

            return new Enumerator(this);
        }

        #endregion

        #region Nested

        private sealed class Enumerator : IEnumerator<QueryResult>
        {
            #region Variables

            private readonly C4QueryEnumerator* _enum;
            [NotNull]private readonly QueryResultSet _parent;

            #endregion

            #region Properties

            object IEnumerator.Current => Current;

            public QueryResult Current
            {
                get {
                    if (_parent._disposed) {
                        throw new ObjectDisposedException(nameof(QueryResultSet));
                    }

                    return new QueryResult(_parent, _enum, _parent._context);
                }
            }

            #endregion

            #region Constructors

            public Enumerator([NotNull]QueryResultSet parent)
            {
                _parent = parent;
                _enum = _parent._c4Enum;
                Log.To.Query.I(Tag, $"Beginning query enumeration ({(long) _enum:x})");
            }

            #endregion

            #region IDisposable

            public void Dispose()
            {
                _parent._threadSafety.DoLocked(() =>
                {
                    if (_parent._disposed) {
                        return;
                    }

                    if (!_parent._randomAccess) {
                        Reset();
                    }
                });
            }

            #endregion

            #region IEnumerator

            public bool MoveNext()
            {
                if (_parent._randomAccess) {
                    throw new InvalidOperationException("Cannot iterate QueryResultSet and also access randomly");
                }

                return _parent._threadSafety.DoLocked(() =>
                {
                    if (_parent._disposed) {
                        return false;
                    }

                    C4Error err;
                    var moved = Native.c4queryenum_next(_enum, &err);
                    if (moved) {
                        return true;
                    }

                    if (err.code != 0) {
                        Log.To.Query.W(Tag, $"{this} error: {err.domain}/{err.code}");
                    } else {
                        Log.To.Query.I(Tag, $"End of query enumeration ({(long) _enum:x})");
                    }

                    return false;
                });
            }

            public void Reset()
            {
                _parent._threadSafety.DoLocked(() =>
                {
                    if (_parent._randomAccess) {
                        throw new InvalidOperationException("Cannot reset QueryResultSet and also access randomly");
                    }

                    if (_parent._disposed || _parent.Count == 0) {
                        return;
                    }

                    LiteCoreBridge.Check(err => Native.c4queryenum_seek(_enum, 0UL, err));
                });
            }

            #endregion
        }

        #endregion
    }
}
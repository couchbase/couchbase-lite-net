// 
//  QueryResultSet.cs
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
using System.Diagnostics;
using System.Linq;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query;

internal sealed unsafe class QueryResultSet : IResultSet
{
    private const string Tag = nameof(QueryResultSet);

    private readonly C4QueryEnumeratorWrapper _c4Enum;
    private readonly QueryResultContext _context;
    private readonly QueryBase _query;
    private readonly ThreadSafety _threadSafety;
    private bool _disposed;
    private bool _enumeratorGenerated;

    internal IDictionary<string, int> ColumnNames { get; }

    internal Collection? Collection => _query.Collection;

    internal Result this[int index]
    {
        get {
            using var threadSafetyScope = _threadSafety.BeginLockedScope();
            if (_disposed) {
                throw new ObjectDisposedException(nameof(QueryResultSet));
            }

            LiteCoreBridge.Check(err => NativeSafe.c4queryenum_seek(_c4Enum, index, err));

            return new Result(this, _c4Enum, _context);
        }
    }

    internal QueryResultSet(QueryBase query, ThreadSafety threadSafety, C4QueryEnumeratorWrapper e,
        IDictionary<string, int> columnNames)
    {
        _query = query;
        _c4Enum = e;
        ColumnNames = columnNames;
        _threadSafety = threadSafety;
        Debug.Assert(query.Database != null);
        _context = new QueryResultContext(query.Database!, e);
    }

    public QueryResultSet? Refresh()
    {
        var newEnum = LiteCoreBridge.CheckTyped(err => 
            _disposed ? null : NativeSafe.c4queryenum_refresh(_c4Enum, err));

        return newEnum != null ? new QueryResultSet(_query, _threadSafety, newEnum, ColumnNames) : null;
    }

    public void Dispose()
    {
        using var threadSafetyScope = _threadSafety.BeginLockedScope();
        if (_disposed) {
            return;
        }

        _disposed = true;
        _context.Dispose();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IEnumerator<Result> GetEnumerator()
    {
        using var threadSafetyScope = _threadSafety.BeginLockedScope();
        if (_disposed) {
            throw new ObjectDisposedException(nameof(QueryResultSet));
        }
        
        if (_enumeratorGenerated) {
            throw new InvalidOperationException(CouchbaseLiteErrorMessage.ResultSetAlreadyEnumerated);
        }

        _enumeratorGenerated = true;
            
        return new Enumerator(this);
    }

    public List<Result> AllResults()
    {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(QueryResultSet));
        }
        
        return this.ToList();
    }

    private sealed class Enumerator : IEnumerator<Result>
    {
        private readonly C4QueryEnumeratorWrapper _enum;
        private readonly QueryResultSet _parent;

        object IEnumerator.Current => Current;

        public Result Current
        {
            get {
                if (_parent._disposed) {
                    throw new ObjectDisposedException(nameof(QueryResultSet));
                }
                
                return new Result(_parent, _enum, _parent._context);
            }
        }

        public Enumerator(QueryResultSet parent)
        {
            _parent = parent;
            _enum = _parent._c4Enum;
            WriteLog.To.Query.I(Tag, $"Beginning query enumeration ({(long) _enum.RawEnumerator:x})");
        }

        public void Dispose()
        {
            // No-op
        }

        public bool MoveNext()
        {
            using var threadSafetyScope = _parent._threadSafety.BeginLockedScope();
            if (_parent._disposed) {
                return false;
            }

            C4Error err;
            var moved = NativeSafe.c4queryenum_next(_enum, &err);
            if (moved) {
                return true;
            }

            if (err.code != 0) {
                WriteLog.To.Query.W(Tag, $"{this} error: {err.domain}/{err.code}");
            } else {
                WriteLog.To.Query.I(Tag, $"End of query enumeration ({(long) _enum.RawEnumerator:x})");
            }

            return false;
        }

        public void Reset()
        {
                
        }
    }
}
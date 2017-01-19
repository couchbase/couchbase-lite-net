//
// LiteCoreDocEnumerator.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Linq;
using System.Threading;
using Couchbase.Lite.Logging;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Querying
{
    internal unsafe abstract class QueryEnumerable<T> : IEnumerable<T>
    {
        private const string Tag = nameof(QueryEnumerable<T>);

        protected readonly Database _db;
        protected readonly C4Query* _query;
        protected readonly C4QueryOptions _options;
        protected readonly string _encodedParameters;

        protected QueryEnumerable(Database db, C4Query* query, C4QueryOptions options, string encodedParameters)
        {
            _db = db;
            _query = query;
            _options = options;
            _encodedParameters = encodedParameters;
        }

        public abstract IEnumerator<T> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    internal unsafe sealed class QueryRowEnumerable : QueryEnumerable<QueryRow>
    {
        internal QueryRowEnumerable(Database db, C4Query* query, C4QueryOptions options, string encodedParameters)
            : base(db, query, options, encodedParameters)
        {

        }

        public override IEnumerator<QueryRow> GetEnumerator()
        {
            return new QueryRowEnumerator(_db, _query, _options, _encodedParameters);
        }
    }

    internal unsafe sealed class LinqQueryEnumerable<T> : QueryEnumerable<T>
    {
        internal LinqQueryEnumerable(Database db, C4Query* query, C4QueryOptions options, string encodedParameters)
            : base(db, query, options, encodedParameters)
        {

        }

        public override IEnumerator<T> GetEnumerator()
        {
            return new LinqQueryEnumerator<T>(_db, _query, _options, _encodedParameters);
        }
    }

    internal unsafe abstract class QueryEnumerator<T> : InteropObject, IEnumerator<T>
    {
        private const string Tag = nameof(QueryEnumerator<T>);
        private long p_native;
        protected C4Query* _query;
        protected Database _db;

        private C4QueryEnumerator* _native
        {
            get {
                return (C4QueryEnumerator*)p_native;
            }
            set {
                p_native = (long)value;
            }
        }

        public T Current { get; protected set; }

        object IEnumerator.Current
        {
            get {
                return Current;
            }
        }

        public QueryEnumerator(Database db, C4Query* query, C4QueryOptions options, string encodedParameters)
        {
            _db = db;
            _query = query;
            _native = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
            {
                var localOpts = options;
                return Native.c4query_run(query, &localOpts, encodedParameters, err);
            });
        }

        protected abstract void SetCurrent(C4QueryEnumerator* enumerator);

        protected override void Dispose(bool finalizing)
        {
            var native = (C4QueryEnumerator*)Interlocked.Exchange(ref p_native, 0);
            if(native != null) {
                Native.c4queryenum_close(native);
                Native.c4queryenum_free(native);
            }
        }

        public bool MoveNext()
        {
            C4Error err;
            if(Native.c4queryenum_next(_native, &err)) {
                SetCurrent(_native);
                return true;
            } else if(err.code != 0) {
                Log.To.Query.E(Tag, $"QueryEnumerator error: {err.domain}/{err.code}");
            }

            return false;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }
    }

    internal sealed unsafe class QueryRowEnumerator : QueryEnumerator<QueryRow>
    {
        public QueryRowEnumerator(Database db, C4Query* query, C4QueryOptions options, string encodedParameters)
            : base(db, query, options, encodedParameters)
        {

        }

        protected override void SetCurrent(C4QueryEnumerator* enumerator)
        {
            Current = enumerator->fullTextTermCount > 0 ? new FullTextQueryRow(_db, _query, enumerator) : new QueryRow(_db, enumerator);
        }
    }

    internal sealed unsafe class LinqQueryEnumerator<T> : QueryEnumerator<T>
    {
        public LinqQueryEnumerator(Database db, C4Query* query, C4QueryOptions options, string encodedParameters)
            : base(db, query, options, encodedParameters)
        {

        }

        protected override void SetCurrent(C4QueryEnumerator* enumerator)
        {
            var doc = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_getBySequence(_db.c4db, enumerator->docSequence, err));
            try {
                FLValue* value = NativeRaw.FLValue_FromTrustedData((FLSlice)doc->selectedRev.body);
                Current = _db.JsonSerializer.Deserialize<T>(value);
            } finally {
                Native.c4doc_free(doc);
            }
        }
    }
}

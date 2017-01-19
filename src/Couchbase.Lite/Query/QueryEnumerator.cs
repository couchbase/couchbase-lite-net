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
    internal unsafe sealed class QueryEnumerator : IEnumerable<QueryRow>
    {
        private const string Tag = nameof(QueryEnumerator);

        private readonly Database _db;
        private readonly C4Query* _query;
        private readonly C4QueryOptions _options;
        private readonly string _encodedParameters;

        internal QueryEnumerator(Database db, C4Query *query, C4QueryOptions options, string encodedParameters)
        {
            _db = db;
            _query = query;
            _options = options;
            _encodedParameters = encodedParameters;
        }

        public IEnumerator<QueryRow> GetEnumerator()
        {
            return new Enumerator(_db, _query, _options, _encodedParameters);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class Enumerator : InteropObject, IEnumerator<QueryRow>
        {
            private long p_native;
            private C4Query* _query;
            private Database _db;

            private C4QueryEnumerator* _native
            {
                get {
                    return (C4QueryEnumerator*)p_native;
                }
                set {
                    p_native = (long)value;
                }
            }

            public QueryRow Current { get; private set; }

            object IEnumerator.Current
            {
                get {
                    return Current;
                }
            }

            public Enumerator(Database db, C4Query* query, C4QueryOptions options, string encodedParameters)
            {
                _db = db;
                _query = query;
                _native = (C4QueryEnumerator*)LiteCoreBridge.Check(err =>
                {
                    var localOpts = options;
                    return Native.c4query_run(query, &localOpts, encodedParameters, err);
                });
            }

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
                    Current = _native->fullTextTermCount > 0 ? new FullTextQueryRow(_db, _query, _native) : new QueryRow(_db, _native);
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
    }
}

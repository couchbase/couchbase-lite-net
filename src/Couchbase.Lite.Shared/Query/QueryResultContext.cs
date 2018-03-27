// 
// QueryResultContext.cs
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
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Interop;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed unsafe class QueryResultContext : DocContext
    {
        #region Variables

        private readonly C4QueryEnumerator* _enumerator;

        #endregion

        #region Constructors

        public QueryResultContext(Database db, C4QueryEnumerator* enumerator)
            : base(db, null)
        {
            _enumerator = enumerator;
        }

        #endregion

        #region Overrides

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Native.c4queryenum_free(_enumerator);
        }

        #endregion
    }
}
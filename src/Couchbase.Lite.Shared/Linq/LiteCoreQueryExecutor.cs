//
//  LiteCoreQueryExecutor.cs
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
#if CBL_LINQ
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Linq;

using LiteCore;
using LiteCore.Interop;

using Newtonsoft.Json;

using Remotion.Linq;

namespace Couchbase.Lite.Internal.Linq
{
    internal class LiteCoreQueryExecutor : IQueryExecutor
    {
        #region Variables

        private readonly Database _db;
        private unsafe C4Query* _query;
        private unsafe C4QueryEnumerator* _queryEnum;

        #endregion

        #region Constructors

        internal LiteCoreQueryExecutor(Database db)
        {
            _db = db;
        }

        #endregion

        #region IQueryExecutor

        public IEnumerable<T> ExecuteCollection<T>(QueryModel queryModel)
        {
            var visitor = new LiteCoreQueryModelVisitor();
            visitor.VisitQueryModel(queryModel);
            var query = visitor.GetJsonQuery();
            CreateQuery(query);
            while (MoveNext()) {
                yield return GetCurrent<T>(visitor.SelectResult);
            }
        }

        public T ExecuteScalar<T>(QueryModel queryModel)
        {
            return ExecuteCollection<T>(queryModel).Single();
        }

        public T ExecuteSingle<T>(QueryModel queryModel, bool returnDefaultWhenEmpty)
        {
            var sequence = ExecuteCollection<T>(queryModel);

            return returnDefaultWhenEmpty ? sequence.SingleOrDefault() : sequence.Single();
        }

        #endregion

        private unsafe void CreateQuery(string queryStr)
        {
            _query = (C4Query*)LiteCoreBridge.Check(err => Native.c4query_new(_db.c4db, queryStr, err));
            _queryEnum = (C4QueryEnumerator*) LiteCoreBridge.Check(err =>
            {
                var opts = C4QueryOptions.Default;
                return Native.c4query_run(_query, &opts, null, err);
            });
        }

        private unsafe bool MoveNext() => Native.c4queryenum_next(_queryEnum, null);

        private unsafe T GetCurrent<T>(ISelectResultContainer resultContainer)
        {
            if (resultContainer != null) {
                resultContainer.Populate(_queryEnum->columns, _db.SharedStrings);
                return (T)resultContainer.Results;
            } else {
                var val = Native.FLArrayIterator_GetValueAt(&_queryEnum->columns, 0);
                using (var reader = new JsonFLValueReader(val, _db.SharedStrings)) {
                    var serializer = JsonSerializer.CreateDefault();
                    return serializer.Deserialize<T>(reader);
                }
            }
        }
    }

    internal sealed class LiteCoreDebugExecutor : IQueryExecutor
    {
        #region IQueryExecutor

        public IEnumerable<T> ExecuteCollection<T>(QueryModel queryModel)
        {
            var visitor = new LiteCoreQueryModelVisitor();
            visitor.VisitQueryModel(queryModel);
            return new[] { visitor.GetJsonQuery() } as IEnumerable<T>;
        }

        public T ExecuteScalar<T>(QueryModel queryModel)
        {
            return ExecuteCollection<T>(queryModel).Single();
        }

        public T ExecuteSingle<T>(QueryModel queryModel, bool returnDefaultWhenEmpty)
        {
            var sequence = ExecuteCollection<T>(queryModel);

            return returnDefaultWhenEmpty ? sequence.SingleOrDefault() : sequence.Single();
        }

        #endregion
    }
}
#endif
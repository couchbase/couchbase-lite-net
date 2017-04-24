//
//  LiteCoreQueryModelVisitor.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
//  
using System.Collections.Generic;

using LiteCore.Interop;
using Newtonsoft.Json;
using Remotion.Linq;
using Remotion.Linq.Clauses;

namespace Couchbase.Lite.Internal.Linq
{
    internal sealed class LiteCoreQueryModelVisitor : QueryModelVisitorBase
    {
        #region Variables

        private readonly IDictionary<string, object> _query = new Dictionary<string, object>();

        #endregion

        #region Public Methods

        public static string GenerateJsonQuery(QueryModel model)
        {
            var visitor = new LiteCoreQueryModelVisitor();
            visitor.VisitQueryModel(model);
            return visitor.GetJsonQuery();
        }

        #endregion

        #region Internal Methods

        internal unsafe string GetJsonQuery()
        {
            var json5 = JsonConvert.SerializeObject(_query);
            return Native.FLJSON5_ToJSON(json5, null);
        }

        #endregion

        #region Overrides

        public override void VisitMainFromClause(MainFromClause fromClause, QueryModel queryModel)
        {
            // No-op, the from source is always the same
        }

        public override void VisitQueryModel(QueryModel queryModel)
        {
            queryModel.SelectClause.Accept(this, queryModel);
            queryModel.MainFromClause.Accept(this, queryModel);
            VisitBodyClauses(queryModel.BodyClauses, queryModel);
            VisitResultOperators(queryModel.ResultOperators, queryModel);
        }

        public override void VisitWhereClause(WhereClause whereClause, QueryModel queryModel, int index)
        {
            _query["WHERE"] = LiteCoreWhereExpressionVisitor.GetJsonExpression(whereClause.Predicate);
            base.VisitWhereClause(whereClause, queryModel, index);
        }

        #endregion
    }
}

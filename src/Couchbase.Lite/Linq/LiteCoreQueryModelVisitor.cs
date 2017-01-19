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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using LiteCore.Interop;
using Newtonsoft.Json;
using Remotion.Linq;
using Remotion.Linq.Clauses;

namespace Couchbase.Lite.Linq
{
    internal sealed class LiteCoreQueryModelVisitor : QueryModelVisitorBase
    {
        private IDictionary<string, object> _query = new Dictionary<string, object>();

        public static string GenerateJsonQuery(QueryModel model)
        {
            var visitor = new LiteCoreQueryModelVisitor();
            visitor.VisitQueryModel(model);
            return visitor.GetJsonQuery();
        }

        public override void VisitQueryModel(QueryModel queryModel)
        {
            queryModel.SelectClause.Accept(this, queryModel);
            queryModel.MainFromClause.Accept(this, queryModel);
            VisitBodyClauses(queryModel.BodyClauses, queryModel);
            VisitResultOperators(queryModel.ResultOperators, queryModel);
        }

        public override void VisitMainFromClause(MainFromClause fromClause, QueryModel queryModel)
        {
            // No-op, the from source is always the same
        }

        public override void VisitWhereClause(WhereClause whereClause, QueryModel queryModel, int index)
        {
            var visitor = new LiteCoreWhereExpressionVisitor();
            visitor.Visit(whereClause.Predicate);
            _query["WHERE"] = visitor.GetJsonExpression();

            base.VisitWhereClause(whereClause, queryModel, index);
        }

        public override void VisitOrderByClause(OrderByClause orderByClause, QueryModel queryModel, int index)
        {
            base.VisitOrderByClause(orderByClause, queryModel, index);
        }

        public override void VisitJoinClause(JoinClause joinClause, QueryModel queryModel, int index)
        {
            base.VisitJoinClause(joinClause, queryModel, index);
        }

        public override void VisitAdditionalFromClause(AdditionalFromClause fromClause, QueryModel queryModel, int index)
        {
            base.VisitAdditionalFromClause(fromClause, queryModel, index);
        }

        public override void VisitGroupJoinClause(GroupJoinClause groupJoinClause, QueryModel queryModel, int index)
        {
            base.VisitGroupJoinClause(groupJoinClause, queryModel, index);
        }

        internal unsafe string GetJsonQuery()
        {
            var json5 = JsonConvert.SerializeObject(_query);
            return Native.FLJSON5_ToJSON(json5, null);
        }
    }
}

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
#if CBL_LINQ
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Couchbase.Lite.Linq;
using Couchbase.Lite.Util;

using LiteCore.Interop;
using Newtonsoft.Json;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.ResultOperators;

namespace Couchbase.Lite.Internal.Linq
{
    internal sealed class LiteCoreQueryModelVisitor : QueryModelVisitorBase
    {
        #region Variables

        private readonly IDictionary<string, object> _query = new Dictionary<string, object>();

        #endregion

        public ISelectResultContainer SelectResult { get; private set; }

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

        public override void VisitWhereClause(WhereClause whereClause, QueryModel queryModel, int index)
        {
            _query["WHERE"] = LiteCoreWhereExpressionVisitor.GetJsonExpression(whereClause.Predicate);
            base.VisitWhereClause(whereClause, queryModel, index);
        }

        public override void VisitOrdering(Ordering ordering, QueryModel queryModel, OrderByClause orderByClause, int index)
        {
            var masterList = new List<object>();
            foreach (var o in orderByClause.Orderings) {
                masterList.AddRange(LiteCoreOrderingExpressionVisitor.GetJsonExpression(o.Expression, o.OrderingDirection));
            }

            _query["ORDER_BY"] = masterList;
            base.VisitOrdering(ordering, queryModel, orderByClause, index);
        }

        public override void VisitSelectClause(SelectClause selectClause, QueryModel queryModel)
        {
            var visitor = new LiteCoreSelectExpressionVisitor(queryModel.BodyClauses.Count > 1);

            visitor.Visit(selectClause.Selector);
            _query["WHAT"] = visitor.GetJsonExpression();
            SelectResult = visitor.SelectResult;

            base.VisitSelectClause(selectClause, queryModel);
        }

        public override void VisitResultOperator(ResultOperatorBase resultOperator, QueryModel queryModel, int index)
        {
            switch (resultOperator) {
                case DistinctResultOperator ro:
                    _query["DISTINCT"] = true;
                    return;
                case AverageResultOperator ro:
                {
                    var select = (_query["WHAT"] as List<object>);
                    select.Insert(0, "AVG()");
                    _query["WHAT"] = new[] { select };
                    return;
                }
                case CountResultOperator ro:
                {
                    var select = (_query["WHAT"] as List<object>);
                    select.Insert(0, "COUNT()");
                    _query["WHAT"] = new[] { select };
                    return;
                }
                case MinResultOperator ro:
                {
                    var select = (_query["WHAT"] as List<object>);
                    select.Insert(0, "MIN()");
                    _query["WHAT"] = new[] { select };
                    return;
                }
                case MaxResultOperator ro:
                {
                    var select = (_query["WHAT"] as List<object>);
                    select.Insert(0, "MAX()");
                    _query["WHAT"] = new[] { select };
                    return;
                }
                case SumResultOperator ro:
                {
                    var select = (_query["WHAT"] as List<object>);
                    select.Insert(0, "SUM()");
                    _query["WHAT"] = new[] { select };
                    return;
                }
                case TakeResultOperator ro:
                    _query["LIMIT"] = ro.GetConstantCount();
                    return;
                case SkipResultOperator ro:
                    _query["OFFSET"] = ro.GetConstantCount();
                    return;
            }

            throw new NotSupportedException($"Result operator {resultOperator.GetType().Name.Replace("ResultOperator","")} not supported");
        }

        public override void VisitJoinClause(JoinClause joinClause, QueryModel queryModel, int index)
        {
            if (!_query.TryGetValue("FROM", out List<Dictionary<string, object>> fromList)) {
                var mainDb = queryModel.MainFromClause.ItemName;
                fromList = new List<Dictionary<string, object>> { new Dictionary<string, object> { ["as"] = mainDb } };
                _query["FROM"] = fromList;
            }

            var innerPath = LiteCoreSelectExpressionVisitor.GetJsonExpression(joinClause.InnerKeySelector, true)[0];
            var outerPath = LiteCoreSelectExpressionVisitor.GetJsonExpression(joinClause.OuterKeySelector, true)[0];

           
            fromList.Add(new Dictionary<string, object>
            {
                ["as"] = joinClause.ItemName,
                ["on"] = new[] { "=", outerPath, innerPath }
            });

            base.VisitJoinClause(joinClause, queryModel, index);
        }

        #endregion
    }
}
#endif
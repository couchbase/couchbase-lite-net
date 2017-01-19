//
//  LiteCoreExpressionTreeVisitor.cs
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
using Newtonsoft.Json;

namespace Couchbase.Lite.Linq
{
    internal sealed class LiteCoreWhereExpressionVisitor : NotSupportedExpressionVisitor
    {
        private static readonly IDictionary<ExpressionType, string> _expressionTypeMap = new Dictionary<ExpressionType, string> {
            [ExpressionType.LessThan] = "<",
            [ExpressionType.LessThanOrEqual] = "<=",
            [ExpressionType.GreaterThan] = ">",
            [ExpressionType.GreaterThanOrEqual] = ">=",
            [ExpressionType.Equal] = "=",
            [ExpressionType.NotEqual] = "!="
        };

        private IList<object> _query = new List<object>();

        public static IList<object> GetJsonExpression(Expression expression)
        {
            var visitor = new LiteCoreWhereExpressionVisitor();
            visitor.Visit(expression);
            return visitor.GetJsonExpression();
        }

        public IList<object> GetJsonExpression()
        {
            if(_query.Count > 1) {
                _query.Insert(0, "AND");
            }
            
            return _query;
        }

        protected override Expression VisitBinary(BinaryExpression expression)
        {
            AppendOperand(expression);
            Visit(expression.Left);
            Visit(expression.Right);

            return expression;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            Visit(node.Operand);
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            (_query.Last() as IList<object>).Add(node.Value);
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            (_query.Last() as IList<object>).Add(new[] { $".{node.Member.Name}" });
            return node;
        }

        private void AppendOperand(BinaryExpression expression)
        {
            if(_expressionTypeMap.ContainsKey(expression.NodeType)) {
                _query.Add(new List<object> { _expressionTypeMap[expression.NodeType] });
                return;
            }

            switch(expression.NodeType) {
                case ExpressionType.AndAlso:
                    break;
                default:
                    base.VisitBinary(expression);
                    break;
            }
        }
    }
}

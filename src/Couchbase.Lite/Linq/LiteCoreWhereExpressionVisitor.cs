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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ResultOperators;

namespace Couchbase.Lite.Linq
{
    internal sealed class LiteCoreWhereExpressionVisitor : NotSupportedExpressionVisitor
    {
        #region Constants

        private static readonly IDictionary<ExpressionType, string> _ExpressionTypeMap = new Dictionary<ExpressionType, string> {
            [ExpressionType.LessThan] = "<",
            [ExpressionType.LessThanOrEqual] = "<=",
            [ExpressionType.GreaterThan] = ">",
            [ExpressionType.GreaterThanOrEqual] = ">=",
            [ExpressionType.Equal] = "=",
            [ExpressionType.NotEqual] = "!="
        };

        #endregion

        #region Variables

        private readonly IDictionary<string, Action<MethodCallExpression>> _methodMap;
        private readonly IList<object> _query = new List<object>();
        private IList<object> _currentExpression;

        private Mode _currentMode;

        #endregion

        #region Constructors

        public LiteCoreWhereExpressionVisitor()
        {
            _methodMap = new Dictionary<string, Action<MethodCallExpression>> {
                ["AnyAndEvery"] = HandleAnyAndEvery,
                ["IsRegexMatch"] = HandleIsRegexMatch,
                ["Contains"] = HandleContains,
                ["Between"] = HandleBetween
            };
        }

        #endregion

        #region Public Methods

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
            
            return _query.First() as IList<object>;
        }

        #endregion

        #region Private Methods

        private void AppendOperand(BinaryExpression expression)
        {
            if(_ExpressionTypeMap.ContainsKey(expression.NodeType)) {
                if(_currentExpression == null) {
                    _currentExpression = new List<object>();
                    _query.Add(_currentExpression);
                }

                _currentExpression.Add(_ExpressionTypeMap[expression.NodeType]);
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

        private void HandleAllAnyEvery(string keyword, Expression part1, Expression part2)
        {
            var overallExpression = new List<object> { keyword, "X" };
            _currentExpression = overallExpression;
            _query.Add(_currentExpression);
            Visit(part1);
            _currentExpression = new List<object>();
            _currentMode = Mode.AllOperator;
            Visit(part2);
            overallExpression.Add(_currentExpression);
        }

        private void HandleAnyAndEvery(MethodCallExpression expression)
        {
            HandleAllAnyEvery("ANY AND EVERY", expression.Arguments[0], expression.Arguments[1]);
        }

        private void HandleBetween(MethodCallExpression expression)
        {
            _currentExpression = new List<object> { "BETWEEN" };
            Visit(expression.Arguments[0]);
            Visit(expression.Arguments[1]);
            Visit(expression.Arguments[2]);
            _query.Add(_currentExpression);
        }

        private void HandleContains(MethodCallExpression expression)
        {
            if(expression.Object.Type != typeof(string)) {
                base.VisitMethodCall(expression);
                return;
            }

            _currentExpression = new List<object> { "CONTAINS()" };
            Visit(expression.Object);
            Visit(expression.Arguments[0]);
            _query.Add(_currentExpression);
        }

        private void HandleIsRegexMatch(MethodCallExpression expression)
        {
            _currentExpression = new List<object> { "REGEXP_LIKE()" };
            Visit(expression.Arguments[0]);
            Visit(expression.Arguments[1]);
            _query.Add(_currentExpression);
        }

        private bool TryHandleAllAny(SubQueryExpression subquery)
        {
            var resultOperator = subquery.QueryModel.ResultOperators[0];
            var allOperator = resultOperator as AllResultOperator;
            var anyOperator = resultOperator as AnyResultOperator;
            if(allOperator != null || anyOperator != null) {
                var last = _currentMode;
                var keyword = allOperator != null ? "EVERY" : "ANY";
                var part1 = subquery.QueryModel.MainFromClause.FromExpression;
                var part2 = allOperator?.Predicate ?? (subquery.QueryModel.BodyClauses.FirstOrDefault() as WhereClause)?.Predicate;
                if(part2 == null) {
                    return false;
                }

                HandleAllAnyEvery(keyword, part1, part2);
                _currentMode = last;
                return true;
            }

            return false;
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException", Justification = "These types are all defined by the type of it they enter")]
        private bool TryHandleFirstLast(SubQueryExpression subquery)
        {
            var resultOperator = subquery.QueryModel.ResultOperators.FirstOrDefault();
            var firstOperator = resultOperator as FirstResultOperator;
            var lastOperator = resultOperator as LastResultOperator;
            if (firstOperator == null && lastOperator == null) {
                return false;
            }

            var from = subquery.QueryModel.MainFromClause.FromExpression;
            _currentExpression = _query.Last() as IList<object>;
            Visit(from);
            var overallExpression = new StringBuilder((_currentExpression.Last() as IList<string>)[0]);
            overallExpression.Append(firstOperator != null ? "[0]" : "[-1]");
            _currentExpression.RemoveAt(_currentExpression.Count - 1);
            _currentExpression.Add(new[] { overallExpression.ToString() });

            return true;
        }

        private bool TryHandleSumOrCount(SubQueryExpression subquery)
        {
            var resultOperator = subquery.QueryModel.ResultOperators[0];
            var countOperator = resultOperator as CountResultOperator;
            var sumOperator = resultOperator as SumResultOperator;
            if(countOperator != null || sumOperator != null) {
                var overallExpression = _query.Last() as IList<object>;
                _currentExpression = new List<object> { countOperator != null ? "ARRAY_COUNT()" : "ARRAY_SUM()" };
                var from = subquery.QueryModel.MainFromClause.FromExpression;
                Visit(from);
                overallExpression.Add(_currentExpression);
                _currentExpression = overallExpression;
                return true;
            }

            return false;
        }

        #endregion

        #region Overrides

        protected override Expression VisitBinary(BinaryExpression expression)
        {
            AppendOperand(expression);
            Visit(expression.Left);
            Visit(expression.Right);

            return expression;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            _currentExpression.Add(node.Value);
            return node;
        }

        protected override Expression VisitExtension(Expression node)
        {
            var subquery = node as SubQueryExpression;
            if(subquery == null) {
                return base.VisitExtension(node);
            }

            if(TryHandleAllAny(subquery)) {
                return node;
            }

            if(TryHandleFirstLast(subquery)) {
                return node;
            }

            if(TryHandleSumOrCount(subquery)) {
                return node;
            }

            return base.VisitExtension(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            Visit(node.Body);
            return node;
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException", Justification = "These types are all defined by the type of it they enter")]
        protected override Expression VisitMember(MemberExpression node)
        {
            var sb = new StringBuilder();

            var currentNode = (Expression)node;
            while(currentNode.NodeType == ExpressionType.MemberAccess || currentNode.NodeType == ExpressionType.Call) {
                if(currentNode.NodeType == ExpressionType.Call) {
                    var ce = (currentNode as MethodCallExpression);
                    if(ce.Method.Name != "get_Item") {
                        return base.VisitMember(node);
                    }
                    
                    var me = ce.Object as MemberExpression;
                    var index = ((ConstantExpression)ce.Arguments[0]).Value;
                    sb.Insert(0, $".{me.Member.Name}[{index}]");
                    currentNode = me.Expression;
                } else {
                    var me = (currentNode as MemberExpression);
                    sb.Insert(0, $".{me.Member.Name}");
                    currentNode = me.Expression;
                }
            }

            if(_currentMode == Mode.AllOperator) {
                sb.Remove(0, 1);
                _currentExpression.Add(new[] { "?X", sb.ToString() });
            } else {
                _currentExpression.Add(new[] { sb.ToString() });
            }

            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Action<MethodCallExpression> handler;
            if (!_methodMap.TryGetValue(node.Method.Name, out handler)) {
                return base.VisitMethodCall(node);
            }

            handler(node);
            return node;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            Visit(node.Operand);
            return node;
        }

        #endregion

        #region Nested

        private enum Mode
        {
            Normal,
            AllOperator
        }

        #endregion
    }
}

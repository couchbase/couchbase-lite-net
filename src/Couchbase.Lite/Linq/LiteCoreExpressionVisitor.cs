// 
//  LiteCoreExpressionVisitor.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ResultOperators;

namespace Couchbase.Lite.Internal.Linq
{
    internal abstract class LiteCoreExpressionVisitor : NotSupportedExpressionVisitor
    {
        #region Constants

        private static readonly IDictionary<ExpressionType, string> _ExpressionTypeMap =
            new Dictionary<ExpressionType, string>
            {
                [ExpressionType.LessThan] = "<",
                [ExpressionType.LessThanOrEqual] = "<=",
                [ExpressionType.GreaterThan] = ">",
                [ExpressionType.GreaterThanOrEqual] = ">=",
                [ExpressionType.Equal] = "=",
                [ExpressionType.NotEqual] = "!=",
                [ExpressionType.Multiply] = "*",
                [ExpressionType.Add] = "+",
                [ExpressionType.Subtract] = "-",
                [ExpressionType.Divide] = "/",
                [ExpressionType.Modulo] = "%",
                [ExpressionType.AndAlso] = "AND",
                [ExpressionType.OrElse] = "OR"
            };

        #endregion

        #region Variables

        private readonly IDictionary<string, Action<MethodCallExpression>> _methodMap;
        protected readonly IList<object> _query = new List<object>();
        private IList<object> _currentExpression;

        private Mode _currentMode;

        #endregion

        private IList<object> CurrentExpression
        {
            get {
                if (_currentExpression == null) {
                    _currentExpression = new List<object>();
                    _query.Add(_currentExpression);
                }

                return _currentExpression;
            }
        }

        protected bool IncludeDbNames { get; set; }

        #region Constructors

        protected LiteCoreExpressionVisitor()
        {
            _methodMap = new Dictionary<string, Action<MethodCallExpression>>
            {
                ["AnyAndEvery"] = HandleAnyAndEvery,
                ["IsMatch"] = HandleIsMatch,
                ["Contains"] = HandleContains,
                ["Between"] = HandleBetween,
                ["Id"] = HandleId,
                ["Sequence"] = HandleSequence,
                ["Like"] = HandleLike,
                ["FullTextMatches"] = HandleFullTextMatches,
                ["FullTextRank"] = HandleFullTextRank,
                ["Abs"] = HandleMath,
                ["Acos"] = HandleMath,
                ["Asin"] = HandleMath,
                ["Atan"] = HandleMath,
                ["Ceiling"] = HandleMath
            };
        }

        #endregion

        #region Private Methods

        private void AppendOperand(BinaryExpression expression)
        {
            if (_ExpressionTypeMap.ContainsKey(expression.NodeType)) {
                if (_currentExpression == null) {
                    _currentExpression = new List<object>();
                    _query.Add(_currentExpression);
                } else {
                    var next = new List<object>();
                    _currentExpression.Add(next);
                    _currentExpression = next;
                }

                _currentExpression.Add(_ExpressionTypeMap[expression.NodeType]);
                return;
            }

            base.VisitBinary(expression);
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
            if (expression.Object.Type != typeof(string)) {
                base.VisitMethodCall(expression);
                return;
            }

            _currentExpression = new List<object> { "CONTAINS()" };
            Visit(expression.Object);
            Visit(expression.Arguments[0]);
            _query.Add(_currentExpression);
        }

        private void HandleId(MethodCallExpression expression)
        {
            CurrentExpression.Add(new[] { "._id" });
        }

        private void HandleIsMatch(MethodCallExpression expression)
        {
            _currentExpression = new List<object> { "MATCH" };
            
            Visit(expression.Arguments[0]);
            Visit(expression.Arguments[1]);
            _query.Add(_currentExpression);
        }

        private void HandleFullTextRank(MethodCallExpression expression)
        {
            _currentExpression = new List<object> { "RANK()" };
            
            Visit(expression.Arguments[0]);
            Visit(expression.Arguments[1]);
            _query.Add(_currentExpression);
        }

        private void HandleMath(MethodCallExpression expression)
        {
            _currentExpression = new List<object> { $"{expression.Method.Name.ToUpperInvariant()}()" };
            Visit(expression.Arguments[0]);
            _query.Add(_currentExpression);
        }

        private void HandleFullTextMatches(MethodCallExpression expression)
        {
            _currentExpression = new List<object> { "REGEXP_LIKE()" };
        }

        private void HandleLike(MethodCallExpression expression)
        {
            _currentExpression = new List<object> { "LIKE" };
            Visit(expression.Arguments[0]);
            Visit(expression.Arguments[1]);
            _query.Add(_currentExpression);
        }

        private void HandleSequence(MethodCallExpression expression)
        {
            CurrentExpression.Add(new[] { "._sequence" });
        }

        private bool TryHandleAll(SubQueryExpression subquery, AllResultOperator ro) => TryHandleAllAny(subquery, ro.Predicate, "EVERY");

        private bool TryHandleAny(SubQueryExpression subquery, AnyResultOperator ro) => TryHandleAllAny(subquery, (subquery.QueryModel.BodyClauses.FirstOrDefault() as WhereClause)?.Predicate, "ANY");

        private bool TryHandleAllAny(SubQueryExpression subquery, Expression predicate, string keyword)
        {
            var last = _currentMode;
            var part1 = subquery.QueryModel.MainFromClause.FromExpression;
            if (predicate == null) {
                return false;
            }


            HandleAllAnyEvery(keyword, part1, predicate);
            _currentMode = last;
            return true;
        }

        private void HandleFirst(SubQueryExpression subquery, FirstResultOperator ro) =>
            HandleFirstLast(subquery, "[0]");

        private void HandleLast(SubQueryExpression subquery, LastResultOperator ro) =>
            HandleFirstLast(subquery, "[-1]");

        [SuppressMessage("ReSharper", "PossibleNullReferenceException", Justification =
            "These types are all defined by the type of it they enter")]
        private void HandleFirstLast(SubQueryExpression subquery, string expressionAddition)
        {
            var from = subquery.QueryModel.MainFromClause.FromExpression;
            _currentExpression = _query.Last() as IList<object>;
            Visit(from);
            var overallExpression = new StringBuilder((_currentExpression.Last() as IList<string>)[0]);
            overallExpression.Append(expressionAddition);
            _currentExpression.RemoveAt(_currentExpression.Count - 1);
            _currentExpression.Add(new[] { overallExpression.ToString() });
        }

        private void HandleSum(SubQueryExpression subquery, SumResultOperator ro) =>
            HandleArrayFunc(subquery, "ARRAY_SUM()");

        private void HandleCount(SubQueryExpression subquery, CountResultOperator ro) =>
            HandleArrayFunc(subquery, "ARRAY_COUNT()");

        private void HandleMax(SubQueryExpression subquery, MaxResultOperator ro) =>
            HandleArrayFunc(subquery, "ARRAY_MAX()");

        private bool TryHandleContains(SubQueryExpression subquery, ContainsResultOperator ro)
        {
            switch (subquery.QueryModel.MainFromClause.FromExpression) {
                case ConstantExpression expr:
                    if (!(expr.Value is IList list)) {
                        return false;
                    }

                    CurrentExpression.Add("IN");
                    Visit(ro.Item);
                    var opList = new List<object> { "[]" };
                    opList.AddRange(list.Cast<object>());
                    CurrentExpression.Add(opList);
                    return true;
                case MemberExpression expr:
                    var last = _currentExpression;
                    CurrentExpression.Add("ARRAY_CONTAINS()");
                    Visit(expr);
                    Visit(ro.Item);
                    _currentExpression = last;
                    return true;
            }

            return false;
        }

        private void HandleArrayFunc(SubQueryExpression subquery, string function)
        {
            var overallExpression = _query.LastOrDefault() as IList<object> ?? _query;
            _currentExpression = new List<object> { function };
            var from = subquery.QueryModel.MainFromClause.FromExpression;
            Visit(from);
            overallExpression.Add(_currentExpression);
            _currentExpression = overallExpression;
        }

        #endregion


        #region Overrides

        protected override Expression VisitBinary(BinaryExpression expression)
        {
            AppendOperand(expression);
            var last = _currentExpression;
            Visit(expression.Left);
            _currentExpression = last;
            Visit(expression.Right);
            _currentExpression = last;

            return expression;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            _currentExpression.Add(node.Value);
            return node;
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (!(node is SubQueryExpression subquery)) {
                return base.VisitExtension(node);
            }

            switch (subquery.QueryModel.ResultOperators.FirstOrDefault()) {
                case null:
                    return base.VisitExtension(node);
                case AllResultOperator ro:
                {
                    if (TryHandleAll(subquery, ro)) {
                        return node;
                    }

                    break;
                }
                case AnyResultOperator ro:
                {
                    if (TryHandleAny(subquery, ro)) {
                        return node;
                    }

                    break;
                }
                case FirstResultOperator ro:
                {
                    HandleFirst(subquery, ro);
                    return node;
                }
                case LastResultOperator ro:
                {
                    HandleLast(subquery, ro);
                    return node;
                }
                case SumResultOperator ro:
                {
                    HandleSum(subquery, ro);
                    return node;
                }
                case CountResultOperator ro:
                {
                    HandleCount(subquery, ro);
                    return node;
                }
                case MaxResultOperator ro:
                {
                    HandleMax(subquery, ro);
                    return node;
                }
                case ContainsResultOperator ro:
                {
                    if (TryHandleContains(subquery, ro)) {
                        return node;
                    }

                    break;
                }
            }

            return base.VisitExtension(node);
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            Visit(node.Expression);
            return node;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            Visit(node.Body);
            return node;
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException", Justification =
            "These types are all defined by the type of it they enter")]
        protected override Expression VisitMember(MemberExpression node)
        {
            var sb = new StringBuilder();

            var currentNode = (Expression) node;
            while (currentNode.NodeType == ExpressionType.MemberAccess || currentNode.NodeType == ExpressionType.Call) {
                if (currentNode.NodeType == ExpressionType.Call) {
                    var ce = (currentNode as MethodCallExpression);
                    if (ce.Method.Name != "get_Item") {
                        return base.VisitMember(node);
                    }

                    var me = ce.Object as MemberExpression;
                    var index = ((ConstantExpression) ce.Arguments[0]).Value;
                    sb.Insert(0, $".{me.Member.Name}[{index}]");
                    currentNode = me.Expression;
                } else {
                    var me = (currentNode as MemberExpression);
                    var mappingProperty =
                        me.Member.GetCustomAttribute(typeof(JsonPropertyAttribute)) as JsonPropertyAttribute;
                    var name = mappingProperty?.PropertyName ?? me.Member.Name;
                    sb.Insert(0, $".{name}");
                    currentNode = me.Expression;
                }
            }

            if (IncludeDbNames && currentNode is QuerySourceReferenceExpression sourceRef) {
                sb.Insert(0, $".{sourceRef.ReferencedQuerySource.ItemName}");
            }

            if (_currentMode == Mode.AllOperator) {
                sb.Remove(0, 1);
                CurrentExpression.Add(new[] { "?X", sb.ToString() });
            } else {
                CurrentExpression.Add(new[] { sb.ToString() });
            }

            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!_methodMap.TryGetValue(node.Method.Name, out var handler)) {
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
#endif
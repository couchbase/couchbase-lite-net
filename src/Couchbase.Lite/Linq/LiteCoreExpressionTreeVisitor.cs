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

namespace Couchbase.Lite.Linq
{
    internal sealed class LiteCoreExpressionTreeVisitor : NotSupportedExpressionVisitor
    {
        private StringBuilder _jsonExpression = new StringBuilder();

        public static string GetJsonExpression(Expression expression)
        {
            var visitor = new LiteCoreExpressionTreeVisitor();
            visitor.Visit(expression);
            return visitor.GetJsonExpression();
        }

        public string GetJsonExpression()
        {
            _jsonExpression.Append("]");
            return _jsonExpression.ToString();
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

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if(node.Method.Name.Equals("Analyze")) {
                var firstArg = (node.Arguments[1] as ConstantExpression).Value as string;
                _jsonExpression.Append($",['.{firstArg}']");
                return node;
            } else if(node.Method.Name.Equals("AllMatch") || node.Method.Name.Equals("AnyMatch")) {
                var firstArg = (node.Arguments[1] as ConstantExpression).Value as string;
                var secondArg = node.Arguments[2] as Expression<Func<object, bool>>;

                var prefix = node.Method.Name.Equals("AllMatch") ? "['EVERY','X'," : "['ANY','X',";
                var childPath = AppendArrayPath(prefix, firstArg);
                var body = secondArg.Body as BinaryExpression;
                AppendOperand(body);
                AppendChildPath(childPath);
                Visit(body.Right);
                _jsonExpression.Append("]");
                return node;
            } else {
                return base.VisitMethodCall(node);
            }
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if(node.Value is string) {
                _jsonExpression.Append($",'{node.Value}'");
            } else {
                _jsonExpression.Append($",{node.Value}");
            }
            return node;
        }

        private string AppendArrayPath(string prefix, string fullPath)
        {
            var lastPeriod = fullPath.LastIndexOf('.');
            var retVal = default(string);
            var arrayPath = fullPath;
            if(lastPeriod != -1) {
                retVal = fullPath.Substring(lastPeriod + 1);
                arrayPath = fullPath.Substring(0, lastPeriod);
            }

            _jsonExpression.Append($"{prefix}['.{arrayPath}'],");
            return retVal;
        }

        private void AppendChildPath(string path)
        {
            if(path == null) {
                _jsonExpression.Append(",['?X']");
            } else {
                _jsonExpression.Append($",['?X','{path}']");
            }
        }

        private void AppendOperand(BinaryExpression expression)
        {
            switch(expression.NodeType) {
                case ExpressionType.Equal:
                    _jsonExpression.Append("['='");
                    break;
                case ExpressionType.LessThan:
                    _jsonExpression.Append("['<'");
                    break;
                case ExpressionType.LessThanOrEqual:
                    _jsonExpression.Append("['<='");
                    break;
                case ExpressionType.GreaterThan:
                    _jsonExpression.Append("['>'");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    _jsonExpression.Append("['>='");
                    break;
                default:
                    base.VisitBinary(expression);
                    break;
            }
        }
    }
}

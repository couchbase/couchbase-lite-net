//
// MapQueryProvider.cs
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
using System.Linq.Expressions;
using System.Text;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Revisions;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Linq
{
    internal sealed class MapQueryProvider : NotSupportedExpressionVisitor, IQueryProvider
    {
        private readonly Database _db;
        private LambdaExpression _where;
        private LambdaExpression _select;
        private LambdaExpression _orderby;
        private string _next;
        private bool _descending;
        private QueryOptions _queryOptions = new QueryOptions ();

        public MapQueryProvider (Database db)
        {
            _db = db;
        }

        public IQueryable CreateQuery (Expression expression)
        {
            throw new NotSupportedException ();
        }

        public IQueryable<TElement> CreateQuery<TElement> (Expression expression)
        {
            Visit (expression);
            return new DatabaseQueryable<TElement> (expression, this);
        }

        public object Execute (Expression expression)
        {
            throw new NotImplementedException ();
        }

        public TResult Execute<TResult> (Expression expression)
        {
            if (_where == null || _select == null) {
                throw new NotSupportedException ();
            }

            var digest = MessageDigest.GetInstance ("SHA-1");
            digest.Update (Encoding.ASCII.GetBytes (expression.ToString ()));
            var digestStr = BitConverter.ToString (digest.Digest ()).Replace ("-", "").ToLowerInvariant ();
            var view = _db.GetView ($"linq_{digestStr}");
            view.SetMap (Map, "1");
            var query = view.CreateQuery ();
            query.Skip = _queryOptions.Skip;
            query.Limit = _queryOptions.Limit;
            var results = query.Run ();
            if (_orderby == null) {
                return (TResult)GetTypedKeys<TResult>(results);
            }

            var realized = ((IEnumerable<QueryRow>)results).ToList ();
            var compiled = _orderby.Compile ();
            realized.Sort ((x, y) => {
                var xVal = compiled.DynamicInvoke(x);
                var yVal = compiled.DynamicInvoke(y);
                var xComp = xVal as IComparable;
                if (xComp != null) {
                    var yComp = yVal as IComparable;
                    return _descending ? yComp.CompareTo(xComp) : xComp.CompareTo (yComp);
                }

                return _descending ? Comparer<object>.Default.Compare (yVal, xVal) : Comparer<object>.Default.Compare(xVal, yVal);
            });

            return (TResult)GetTypedKeys<TResult>(realized);
        }

        private object GetTypedKeys<TOriginal> (IEnumerable<QueryRow> input)
        {
            var genericTypeArgs = typeof (TOriginal).GetGenericArguments ();
            var tmp = input.Select (x => x.Key);
            if (genericTypeArgs.Length == 0) {
                return tmp;
            }

            if (genericTypeArgs [0].Name == "RevisionID") {
                tmp = input.Select (x => ((string)x.Key).AsRevID());
            }

            var methodInfo = typeof (Enumerable).GetMethod ("Cast").MakeGenericMethod(genericTypeArgs[0]);
            return methodInfo.Invoke (null, new [] { tmp });
        }

        protected override Expression VisitMethodCall (MethodCallExpression node)
        {
            _next = node.Method.Name;
            Visit (node.Arguments [1]);
            Visit (node.Arguments [0]);

            return node;
        }


        protected override Expression VisitUnary (UnaryExpression node)
        {
            if (_next == "Select") {
                _select = (LambdaExpression)node.Operand;
            } else if (_next == "Where") {
                _where = (LambdaExpression)node.Operand;
            } else if (_next == "OrderBy" || _next == "OrderByDescending") {
                _orderby = (LambdaExpression)node.Operand;
                _descending = _next == "OrderByDescending";
            } else if(_next == "GroupBy") { 

            } else {
                throw new NotSupportedException ();
            }

            return node;
        }

        protected override Expression VisitConstant (ConstantExpression node)
        {
            if (_next == "Skip") {
                _queryOptions.Skip = (int)node.Value;
                return node;
            } else if (_next == "Take") {
                _queryOptions.Limit = (int)node.Value;
                return node;
            }

            _fromSubclass = true;
            return base.VisitConstant (node);
        }

        private void Map (IDictionary<string, object> doc, EmitDelegate emit)
        {
            var mapFunction = (Func<QueryRow, bool>)_where.Compile ();
            var selectFunction = _select != null ? (Func<QueryRow, object>)_select.Compile () : (Func<QueryRow, object>)_orderby.Compile();
            var fakeRow = new QueryRow (doc.CblID (), 0, null, null, new RevisionInternal (doc), null);
            if (mapFunction (fakeRow)) {
                emit (selectFunction (fakeRow), null);
            }
        }
    }
}


//
// MapQueryProvider.cs
//
// Author:
//  Jim Borden  <jim.borden@couchbase.com>
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
#if !NET_3_5
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
    internal sealed class MapReduceQueryProvider : NotSupportedExpressionVisitor, IQueryProvider
    {
        private readonly Database _db;
        private readonly string _version;
        private LambdaExpression _where;
        private LambdaExpression _select;
        private LambdaExpression _orderby;
        private LambdaExpression _reduce;
        private string _next;
        private bool _descending;
        private QueryOptions _queryOptions;

        public MapReduceQueryProvider(Database db, string version)
        {
            _db = db;
            _version = version;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            throw new NotSupportedException();
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new DatabaseQueryable<TElement>(expression, this);
        }

        public object Execute(Expression expression)
        {
            throw new NotImplementedException();
        }

        public TResult Execute<TResult>(Expression expression)
        {
            _queryOptions = new QueryOptions();
            _descending = false;
            Visit(expression);

            if(_select == null) {
                throw new NotSupportedException("Couchbase LINQ expressions require a Select statement");
            }

            if(_reduce != null && _orderby != null) {
                throw new InvalidOperationException("Cannot both reduce and sort the same query");
            }

            var query = default(Query);
            if(_where != null) {
                var view = SetupView();
                query = view.CreateQuery();
            } else {
                query = _db.CreateAllDocumentsQuery();
            }

            query.Skip = _queryOptions.Skip;
            query.Limit = _queryOptions.Limit;
            query.Descending = _queryOptions.Descending;

            var results = default(QueryEnumerator);
            if(_orderby == null) {
                results = query.Run();
                return _reduce != null ? (TResult)results.First().Value
                                                          : (TResult)GetTypedKeys<TResult>(results);
            }

            // OrderBy needs to defer skip and limit to the end
            var skip = query.Skip;
            var limit = query.Limit;
            query.Skip = 0;
            query.Limit = Int32.MaxValue;
            results = query.Run();

            var realized = ((IEnumerable<QueryRow>)results).ToList();
            var compiled = _orderby.Compile();
            realized.Sort((x, y) => {
                var xVal = compiled.DynamicInvoke(x);
                var yVal = compiled.DynamicInvoke(y);
                var xComp = xVal as IComparable;
                if(xComp != null) {
                    var yComp = yVal as IComparable;
                    return _descending ? yComp.CompareTo(xComp) : xComp.CompareTo(yComp);
                }

                return _descending ? Comparer<object>.Default.Compare(yVal, xVal) : Comparer<object>.Default.Compare(xVal, yVal);
            });

            return (TResult)GetTypedKeys<TResult>(realized, skip, limit);
        }

        private View SetupView()
        {
            var digest = MessageDigest.GetInstance("SHA-1");

            digest.Update(Encoding.UTF8.GetBytes(_select.ToString()));
            digest.Update(Encoding.UTF8.GetBytes(_where.ToString()));

            var digestStr = BitConverter.ToString(digest.Digest()).Replace("-", "").ToLowerInvariant();
            var view = _db.GetView($"linq_{digestStr}");
            var reduce = _reduce != null ? Reduce : (ReduceDelegate)null;
            view.SetMapReduce(Map, reduce, _version);
            return view;
        }

        private object GetTypedKeys<TOriginal>(IEnumerable<QueryRow> input, int skip = 0, int limit = Int32.MaxValue)
        {
            var genericTypeArgs = typeof(TOriginal).GetGenericArguments();
            var tmp = _where != null ? input.Skip(skip).Take(limit).Select(x => x.Key) : Map(input.Skip(skip).Take(limit));

            if(genericTypeArgs.Length == 0) {
                return tmp;
            }

            var methodInfo = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(genericTypeArgs[0]);
            return methodInfo.Invoke(null, new[] { tmp });
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            _next = node.Method.Name;
            if(node.Arguments.Count > 1) {
                Visit(node.Arguments[1]);
            } else if(_next == "Reverse") {
                _queryOptions.Descending = true;
            }

            Visit(node.Arguments[0]);
            return node;
        }


        protected override Expression VisitUnary(UnaryExpression node)
        {
            if(_next == "Select") {
                _select = (LambdaExpression)node.Operand;
            } else if(_next == "Where") {
                _where = (LambdaExpression)node.Operand;
            } else if(_next == "OrderBy" || _next == "OrderByDescending") {
                _orderby = (LambdaExpression)node.Operand;
                _descending = _next == "OrderByDescending";
            } else if(_next == "Aggregate") {
                _reduce = (LambdaExpression)node.Operand;
            } else {
                throw new NotSupportedException();
            }

            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if(_next == "Skip") {
                _queryOptions.Skip = (int)node.Value;
                return node;
            } else if(_next == "Take") {
                _queryOptions.Limit = (int)node.Value;
                return node;
            }

            _fromSubclass = true;
            return base.VisitConstant(node);
        }

        private IEnumerable Map(IEnumerable<QueryRow> rows)
        {
            var selectFunction = _select.Compile();
            foreach(var row in rows) {
                yield return selectFunction.DynamicInvoke(row);
            }
        }

        private void Map(IDictionary<string, object> doc, EmitDelegate emit)
        {
            var mapFunction = (Func<QueryRow, bool>)_where.Compile();
            var selectFunction = _select.Compile();
            var fakeRow = new QueryRow(doc.CblID(), doc.GetCast<long>("_local_seq"), null, null, new RevisionInternal(doc), null);
            if(mapFunction(fakeRow)) {
                emit(selectFunction.DynamicInvoke(fakeRow), null);
            }
        }

        private object Reduce(IEnumerable<object> keys, IEnumerable<object> values, bool rereduce)
        {
            var reduceFunction = _reduce.Compile();
            var type = keys.FirstOrDefault()?.GetType();
            object reduced = (type != null && type.IsValueType) ? Activator.CreateInstance(type) : null;
            foreach(var k in keys) {
                reduced = reduceFunction.DynamicInvoke(reduced, k);
            }

            return reduced;
        }
    }
}

#endif
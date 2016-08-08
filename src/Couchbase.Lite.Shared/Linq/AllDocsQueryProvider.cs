//
// AllDocsQueryProvider.cs
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

namespace Couchbase.Lite.Linq
{
    public class AllDocsQueryProvider : IQueryProvider
    {
        private readonly Database _db;

        public AllDocsQueryProvider (Database db)
        {
            _db = db;
        }

        public IQueryable CreateQuery (Expression expression)
        {
            throw new NotImplementedException ();
        }

        public IQueryable<TElement> CreateQuery<TElement> (Expression expression)
        {
            throw new NotImplementedException ();
        }

        public object Execute (Expression expression)
        {
            throw new NotImplementedException ();
        }

        public TResult Execute<TResult> (Expression expression)
        {
            var elementType = typeof (TResult).GenericTypeArguments [0];
            return (TResult)Execute (expression, elementType);
        }

        private object Execute (Expression expression, Type elementType)
        {
            var method = expression as MethodCallExpression;
            if (method == null) {
                throw new NotSupportedException ();
            }

            if (method.Method.Name != "Select") {
                throw new NotSupportedException ();
            }



            var lambda = (LambdaExpression)((UnaryExpression)method.Arguments [1]).Operand;
            var selectFunction = lambda.Compile ();
            var queryResults = _db.CreateAllDocumentsQuery ().Run ();
            var results = (IList)Activator.CreateInstance (typeof (List<>).MakeGenericType (elementType));
            foreach (var result in queryResults) {
                var selected = selectFunction.DynamicInvoke (result);
                results.Add (selected);
            }

            return results;
        }
    }
}


//
// DatabaseQueryProvider.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Linq
{

    internal sealed class DatabaseQueryProvider : IQueryProvider
    {
        private readonly Database _db;
        private View _view;
        private LambdaExpression _lambda;

        public DatabaseQueryProvider (Database db)
        {
            _db = db;
        }

        public IQueryable CreateQuery (Expression expression)
        {
            return null;
        }

        public IQueryable<TElement> CreateQuery<TElement> (Expression expression)
        {
            var method = expression as MethodCallExpression;
            if (method == null) {
                throw new NotSupportedException ();
            }

            if (method.Method.Name == "Select") {
                return new DatabaseQueryable<TElement> (expression, new AllDocsQueryProvider(_db));
            }

            if (method.Method.Name == "Where") {
                return new DatabaseQueryable<TElement> (expression, new MapQueryProvider (_db));
            }

            throw new NotSupportedException ();
        }

        public object Execute (Expression expression)
        {
            throw new NotSupportedException ();
        }

        public TResult Execute<TResult> (Expression expression)
        {
            throw new NotSupportedException ();
        }
    }
}


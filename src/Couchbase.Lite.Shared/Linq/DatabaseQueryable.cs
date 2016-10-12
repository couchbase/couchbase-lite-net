//
// DatabaseQueryable.cs
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
#if !NET_3_5
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Couchbase.Lite.Linq
{
    internal sealed class DatabaseQueryable<TElement> : IOrderedQueryable<TElement>
    {
        private readonly string _version;

        public Type ElementType {
            get {
                return typeof (TElement);
            }
        }

        public Expression Expression {
            get; 
        }

        public IQueryProvider Provider {
            get;
        }

        public DatabaseQueryable (Database db, string version)
        {
            Expression = Expression.Constant (this);
            Provider = new MapReduceQueryProvider (db, version);
        }

        public DatabaseQueryable (Expression expression, IQueryProvider provider)
        {
            Expression = expression;
            Provider = provider;
        }

        public IEnumerator<TElement> GetEnumerator ()
        {
            return (Provider.Execute<IEnumerable<TElement>>(Expression)).GetEnumerator ();
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator();
        }
    }
}
#endif
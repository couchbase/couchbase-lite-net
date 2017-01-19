//
//  Database_Linq.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Lite.Linq;
using Remotion.Linq;
using Remotion.Linq.Parsing.Structure;

namespace Couchbase.Lite
{
    public static class QueryableFactory
    {
        public static IQueryable<TElement> MakeQueryable<TElement>(Database db)
        {
            return new DatabaseQueryable<TElement>(db);
        }

        internal static IQueryable<string> MakeDebugQueryable()
        {
            return new DatabaseDebugQueryable();
        }
    }

    internal sealed class DatabaseQueryable<TElement> : QueryableBase<TElement>
    {
        public DatabaseQueryable(Database db)
            : this(QueryParser.CreateDefault(), new LiteCoreQueryExecutor(db))
        {
            
        }

        public DatabaseQueryable(IQueryParser queryParser, IQueryExecutor executor)
            : base(new DefaultQueryProvider(typeof(DatabaseQueryable<>), queryParser, executor))
        {
            
        }

        public DatabaseQueryable(IQueryProvider provider, Expression expression)
            : base(provider, expression)
        {
        }
    }

    internal sealed class DatabaseDebugQueryable : QueryableBase<string>
    {
        public DatabaseDebugQueryable()
            : this(QueryParser.CreateDefault(), new LiteCoreDebugExecutor())
        {

        }

        public DatabaseDebugQueryable(IQueryParser queryParser, IQueryExecutor executor)
            : base(new DefaultQueryProvider(typeof(DatabaseQueryable<>), queryParser, executor))
        {

        }

        public DatabaseDebugQueryable(IQueryProvider provider, Expression expression)
            : base(provider, expression)
        {
        }
    }
}

// 
// DatabaseQueryable.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
//using System.Linq;
//using System.Linq.Expressions;
//using Couchbase.Lite.Internal.Linq;
//using Remotion.Linq;
//using Remotion.Linq.Parsing.Structure;

//namespace Couchbase.Lite.Internal.Query
//{
//    internal sealed class DatabaseQueryable<TElement> : QueryableBase<TElement> where TElement : class, IDocumentModel, new()
//    {
//        #region Constructors

//        public DatabaseQueryable(Database db, bool prefetch)
//            : base(QueryParser.CreateDefault(), new LiteCoreQueryExecutor(db, prefetch))
//        {
            
//        }

//        public DatabaseQueryable(IQueryProvider provider, Expression expression)
//            : base(provider, expression)
//        {
//        }

//        #endregion
//    }

//    internal sealed class DatabaseDebugQueryable : QueryableBase<string>
//    {
//        #region Constructors

//        public DatabaseDebugQueryable()
//            : this(QueryParser.CreateDefault(), new LiteCoreDebugExecutor())
//        {

//        }

//        public DatabaseDebugQueryable(IQueryParser queryParser, IQueryExecutor executor)
//            : base(new DefaultQueryProvider(typeof(DatabaseQueryable<>), queryParser, executor))
//        {

//        }

//        public DatabaseDebugQueryable(IQueryProvider provider, Expression expression)
//            : base(provider, expression)
//        {
//        }

//        #endregion
//    }
//}

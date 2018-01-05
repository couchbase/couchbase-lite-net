// 
//  LiteCoreSelectExpressionVisitor.cs
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
using System.Linq.Expressions;

using Couchbase.Lite.Internal.Linq;

using JetBrains.Annotations;

using Remotion.Linq.Clauses.Expressions;

namespace Couchbase.Lite.Linq
{
    internal sealed class LiteCoreSelectExpressionVisitor : LiteCoreExpressionVisitor
    {

        public LiteCoreSelectExpressionVisitor(bool includeDbNames)
        {
            IncludeDbNames = includeDbNames;
        }

        #region Public Methods

        public static IList<object> GetJsonExpression(Expression expression, bool includeDbNames)
        {
            var visitor = new LiteCoreSelectExpressionVisitor(includeDbNames);
            visitor.Visit(expression);
            return visitor.GetJsonExpression();
        }

        public IList<object> GetJsonExpression()
        {
            return _query;
        }

        #endregion

        public ISelectResultContainer SelectResult { get; private set; }

        protected override Expression VisitExtension(Expression node)
        {
            if (node is QuerySourceReferenceExpression exp) {
                _query.Add(new[] { "." });
                return node;
            }

            return base.VisitExtension(node);
        }

        protected override Expression VisitNewArray(NewArrayExpression node)
        {
            SelectResult = new SelectResultListContainer(node.Type.GetElementType());
            foreach (var expr in node.Expressions) {
                Visit(expr);
            }

            return node;
        }

        protected override Expression VisitNew(NewExpression node)
        {
            SelectResult = new SelectResultAnonymousContainer(node.Constructor);
            foreach (var expr in node.Arguments) {
                Visit(expr);
            }

            return node;
        }
    }
}
#endif
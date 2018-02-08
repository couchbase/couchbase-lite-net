// 
//  LiteCoreOrderingExpressionVisitor.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Remotion.Linq.Clauses;

namespace Couchbase.Lite.Internal.Linq
{
    internal sealed class LiteCoreOrderingExpressionVisitor : LiteCoreExpressionVisitor
    {
        #region Public Methods

        public static IList<object> GetJsonExpression(Expression expression, OrderingDirection direction)
        {
            var visitor = new LiteCoreOrderingExpressionVisitor();
            visitor.Visit(expression);
            return visitor.GetJsonExpression(direction);
        }

        public IList<object> GetJsonExpression(OrderingDirection direction)
        {
            var retVal = _query.First() as IList<object>;
            if (direction == OrderingDirection.Asc) {
                return retVal;
            }

            retVal.Insert(0, "DESC");
            return new List<object> { retVal };
        }

        #endregion
    }
}
#endif
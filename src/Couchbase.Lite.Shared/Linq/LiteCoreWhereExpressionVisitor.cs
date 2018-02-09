//
//  LiteCoreExpressionTreeVisitor.cs
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ResultOperators;

namespace Couchbase.Lite.Internal.Linq
{
    internal sealed class LiteCoreWhereExpressionVisitor : LiteCoreExpressionVisitor
    {


        #region Public Methods

        public static IList<object> GetJsonExpression(Expression expression)
        {
            var visitor = new LiteCoreWhereExpressionVisitor();
            visitor.Visit(expression);
            return visitor.GetJsonExpression();
        }

        public IList<object> GetJsonExpression()
        {
            if(_query.Count > 1) {
                _query.Insert(0, "AND");
            }
            
            return _query.First() as IList<object>;
        }

        #endregion


  
    }
}
#endif
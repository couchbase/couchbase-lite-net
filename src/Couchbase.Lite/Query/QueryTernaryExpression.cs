// 
//  QueryTernaryExpression.cs
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

using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QueryTernaryExpression : QueryExpression, IArrayExpressionIn, IArrayExpressionSatisfies
    {
        #region Variables

        private readonly string _function;
        private readonly string _variableName;
        private object _in;
        private QueryExpression _predicate;

        #endregion

        #region Constructors

        internal QueryTernaryExpression(string function, string variableName)
        {
            _function = function;
            _variableName = variableName;
        }

        #endregion

        #region Overrides

        protected override object ToJSON()
        {
            var inObj = _in;
            if (_in is QueryExpression e) {
                inObj = e.ConvertToJSON();
            }

            return new[] {
                _function,
                _variableName,
                inObj,
                _predicate?.ConvertToJSON()
            };
        }

        #endregion

        #region IArrayExpressionIn

        public IArrayExpressionSatisfies In(object expression)
        {
            _in = expression;
            return this;
        }

        #endregion

        #region IArrayExpressionSatisfies

        public IExpression Satisfies(IExpression expression)
        {
            _predicate = Misc.TryCast<IExpression, QueryExpression>(expression);
            return this;
        }

        #endregion
    }
}
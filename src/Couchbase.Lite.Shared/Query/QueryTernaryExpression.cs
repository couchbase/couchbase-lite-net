﻿// 
//  QueryTernaryExpression.cs
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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;
using JetBrains.Annotations;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QueryTernaryExpression : QueryExpression, IArrayExpressionIn, IArrayExpressionSatisfies
    {
        #region Constants

        private const string Tag = nameof(QueryTernaryExpression);

        #endregion

        #region Variables

        private readonly string _function;
        private readonly IVariableExpression _variableName;
        private IExpression _in;
        private QueryExpression _predicate;

        #endregion

        #region Constructors

        internal QueryTernaryExpression(string function, IVariableExpression variableName)
        {
            _function = function;
            _variableName = variableName;
        }

        #endregion

        #region Overrides

        protected override object ToJSON()
        {
            var inObj = Misc.TryCast<IExpression, QueryExpression>(_in);
            var variableName = Misc.TryCast<IVariableExpression, QueryTypeExpression>(_variableName);

            return new[] {
                _function,
                variableName.KeyPath,
                inObj.ConvertToJSON(),
                _predicate?.ConvertToJSON()
            };
        }

        #endregion

        #region IArrayExpressionIn

        public IArrayExpressionSatisfies In([NotNull]IExpression expression)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression);
            _in = expression;
            return this;
        }

        #endregion

        #region IArrayExpressionSatisfies

        public IExpression Satisfies([NotNull]IExpression expression)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression);
            _predicate = Misc.TryCast<IExpression, QueryExpression>(expression);
            return this;
        }

        #endregion
    }
}
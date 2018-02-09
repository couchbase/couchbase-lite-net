// 
//  QueryConstantExpression.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
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

using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal abstract class QueryConstantExpressionBase : QueryExpression
    {

    }

    internal sealed class QueryConstantExpression<T> : QueryConstantExpressionBase
    {
        #region Variables

        private readonly T _internal;

        #endregion

        #region Constructors

        public QueryConstantExpression(T obj)
        {
            _internal = obj;
        }

        #endregion

        #region Overrides

        protected override object ToJSON() => _internal;

        #endregion
    }
}
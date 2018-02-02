// 
//  QueryCompoundExpression.cs
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

using System.Collections.Generic;

using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal class QueryCompoundExpression : QueryExpression, IFullTextExpression
    {
        #region Variables

        private readonly string _operation;
        private readonly IExpression[] _subpredicates;

        #endregion

        #region Constructors

        public QueryCompoundExpression(string op, params IExpression[] subpredicates)
        {
            _operation = op;
            _subpredicates = subpredicates;
        }

        #endregion

        #region Overrides

        protected override object ToJSON()
        {
            var obj = new List<object> { _operation };
            foreach (var subp in _subpredicates) {
                var queryExp = Misc.TryCast<IExpression, QueryExpression>(subp);
                obj.Add(queryExp.ConvertToJSON());
            }

            return obj;
        }

        public override string ToString() => JsonConvert.SerializeObject(ConvertToJSON());

        #endregion

        #region IFullTextExpression

        public IExpression Match(string query)
        {
            _subpredicates[_subpredicates.Length - 1] = Expression.String(query);
            return this;
        }

        #endregion
    }
}

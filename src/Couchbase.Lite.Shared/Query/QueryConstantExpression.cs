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

using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Query;

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

        #region Private Methods

        private object DictAsJson(IDictionary<string, object> dictionary)
        {
            return dictionary.ToDictionary(x => x.Key, x => ToJSON(x.Value));
        }

        private object ListAsJson(IList list)
        {
            var retVal = new List<object> { "[]" };
            retVal.AddRange(list.Cast<object>().Select(ToJSON));
            return retVal;
        }

        private object ToJSON(object input)
        {
            switch (input) {
                case IDictionary<string, object> d:
                    return DictAsJson(d);
                case IList e:
                    return ListAsJson(e);
                case IExpression qe when qe is QueryExpression qe2:
                    return qe2.ConvertToJSON();
                default:
                    return DataOps.ToCouchbaseObject(input);
            }
        }

        #endregion

        #region Overrides

        protected override object ToJSON() => ToJSON(_internal);

        #endregion
    }
}
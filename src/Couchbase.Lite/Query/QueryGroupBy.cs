// 
// GroupBy.cs
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
using System.Collections.Generic;
using System.Linq;
using Couchbase.Lite.Query;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QueryGroupBy : LimitedQuery, IGroupBy
    {
        #region Variables

        private readonly IExpression _expression;
        private readonly IList<IGroupBy> _groupings;

        #endregion

        #region Constructors

        internal QueryGroupBy(IList<IGroupBy> groupBy)
        {
            _groupings = groupBy;
            GroupByImpl = this;
        }

        internal QueryGroupBy(XQuery query, IList<IGroupBy> groupBy)
            : this(groupBy)
        {
            Copy(query);
            GroupByImpl = this;
        }

        internal QueryGroupBy(IExpression expression)
        {
            _expression = expression;
            GroupByImpl = this;
        }

        #endregion

        #region Public Methods

        public object ToJSON()
        {
            var exp = _expression as QueryExpression;
            if (exp != null) {
                return exp.ConvertToJSON();
            }

            var obj = new List<object>();
            foreach (var o in _groupings.OfType<QueryGroupBy>()) {
                obj.Add(o.ToJSON());
            }

            return obj;
        }

        #endregion

        #region IHavingRouter

        public IHaving Having(IExpression expression)
        {
            return new Having(this, expression);
        }

        #endregion

        #region IOrderByRouter

        public IOrdering OrderBy(params IOrdering[] ordering)
        {
            return new QueryOrdering(this, ordering);
        }

        #endregion
    }
}

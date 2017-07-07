// 
// Ordering.cs
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
    internal class QueryOrdering : LimitedQuery, IOrdering
    {
        #region Properties

        internal IList<IOrdering> Orders { get; }

        #endregion

        #region Constructors

        internal QueryOrdering(IList<IOrdering> orderBy)
        {
            Orders = orderBy;
            OrderingImpl = this;
        }

        internal QueryOrdering(XQuery query, IList<IOrdering> orderBy)
            : this(orderBy)
        {
            Copy(query);
            OrderingImpl = this;
        }

        #endregion

        public virtual object ToJSON()
        {
            var obj = new List<object>();
            foreach (var o in Orders.OfType<QueryOrdering>()) {
                obj.Add(o.ToJSON());
            }

            return obj;
        }
    }

    internal sealed class SortOrder : QueryOrdering, ISortOrder
    {
        #region Properties

        internal IExpression Expression { get; }

        internal bool IsAscending { get; private set; }

        #endregion

        #region Constructors

        public SortOrder(IExpression expression) : base(null)
        {
            IsAscending = true;
            Expression = expression;
        }

        #endregion

        public override object ToJSON()
        {
            var obj = new List<object>();
            var exp = Expression as QueryExpression;
            if (exp != null) {
                if (!IsAscending) {
                    obj.Add("DESC");
                } else {
                    return exp.ConvertToJSON();
                }

                obj.Add(exp.ConvertToJSON());
            }

            return obj;
        }

        #region ISortOrder

        public IOrdering Ascending()
        {
            IsAscending = true;
            return this;
        }

        public IOrdering Descending()
        {
            IsAscending = false;
            return this;
        }

        #endregion
    }
}

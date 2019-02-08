// 
// Ordering.cs
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
using System.Diagnostics;
using System.Linq;

using Couchbase.Lite.Query;
using JetBrains.Annotations;

namespace Couchbase.Lite.Internal.Query
{
    internal class QueryOrderBy : LimitedQuery, IOrderBy
    {
        #region Properties

        internal IList<IOrdering> Orders { get; }

        #endregion

        #region Constructors

        internal QueryOrderBy(IList<IOrdering> orderBy)
        {
            Orders = orderBy;
            OrderByImpl = this;
        }

        internal QueryOrderBy(XQuery query, IList<IOrdering> orderBy)
            : this(orderBy)
        {
            Copy(query);
            OrderByImpl = this;
        }

        #endregion

        public virtual object ToJSON()
        {
            var obj = new List<object>();
            foreach (var o in Orders.OfType<QueryOrderBy>()) {
                obj.Add(o.ToJSON());
            }

            return obj;
        }
    }

    internal sealed class SortOrder : QueryOrderBy, ISortOrder
    {
        #region Properties

        internal IExpression Expression { get; }

        internal bool IsAscending { get; private set; }

        #endregion

        #region Constructors

        internal SortOrder([NotNull]IExpression expression) : base(null)
        {
            Debug.Assert(expression != null);
            IsAscending = true;
            Expression = expression;
        }

        #endregion

        public override object ToJSON()
        {
            var obj = new List<object>();
            if (Expression is QueryExpression exp) {
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

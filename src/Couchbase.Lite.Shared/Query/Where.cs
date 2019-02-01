// 
// Where.cs
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
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;
using JetBrains.Annotations;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class Where : LimitedQuery, IWhere
    {
        #region Constants

        private const string Tag = nameof(Where);

        #endregion

        #region Constructors

        internal Where(XQuery query, IExpression expression)
        {
            Copy(query);
            WhereImpl = expression as QueryExpression;
        }

        #endregion

        #region IGroupByRouter

        public IGroupBy GroupBy([ItemNotNull]params IExpression[] expressions)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expressions), expressions);
            return new QueryGroupBy(this, expressions);
        }

        #endregion

        #region IOrderByRouter

        public IOrderBy OrderBy([ItemNotNull]params IOrdering[] orderings)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(orderings), orderings);
            return new QueryOrderBy(this, orderings);
        }

        #endregion
    }
}

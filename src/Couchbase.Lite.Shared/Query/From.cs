// 
//  From.cs
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

using System.Diagnostics;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class From : LimitedQuery, IFrom
    {
        #region Constants

        private const string Tag = nameof(From);

        #endregion

        #region Constructors

        internal From([NotNull]XQuery query, IDataSource impl)
        {
            Debug.Assert(query != null);

            Copy(query);

            FromImpl = impl as QueryDataSource;
            Database = (impl as DatabaseSource)?.Database;
        }

        #endregion

        #region Public Methods

        public object ToJSON()
        {
            return FromImpl?.ToJSON();
        }

        #endregion

        #region IGroupByRouter

        public IGroupBy GroupBy([NotNull]params IExpression[] expressions)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expressions), expressions);
            ValidateParams(expressions);
            return new QueryGroupBy(this, expressions);
        }

        #endregion

        #region IJoinRouter

        public IJoins Join([NotNull]params IJoin[] joins)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(joins), joins);
            ValidateParams(joins);
            return new QueryJoin(this, joins);
        }

        #endregion

        #region IOrderByRouter

        public IOrderBy OrderBy([NotNull]params IOrdering[] orderings)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(orderings), orderings);
            ValidateParams(orderings);
            return new QueryOrderBy(this, orderings);
        }

        #endregion

        #region IWhereRouter

        [NotNull]
        public IWhere Where([NotNull]IExpression expression)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression);
            return new Where(this, expression);
        }

        #endregion
    }
}

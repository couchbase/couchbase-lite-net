﻿// 
//  QueryJoin.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using JetBrains.Annotations;
using Debug = System.Diagnostics.Debug;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QueryJoin : LimitedQuery, IJoins, IJoinOn
    {
        #region Constants

        private const string Tag = nameof(QueryJoin);

        #endregion

        #region Variables

        private readonly IList<IJoin> _joins;
        private readonly string _joinType;
        private readonly IDataSource _source;
        private IExpression _on;

        #endregion

        #region Constructors

        internal QueryJoin(IList<IJoin> joins)
        {
            _joins = joins;
            JoinImpl = this;
        }

        internal QueryJoin([NotNull]XQuery source, IList<IJoin> joins)
        {
            Debug.Assert(source != null);

            Copy(source);
            _joins = joins; 
            JoinImpl = this;
        }

        internal QueryJoin(string joinType, IDataSource dataSource)
        {
            _joinType = joinType;
            _source = dataSource;
        }

        #endregion

        #region Public Methods

        public object ToJSON()
        {
            if (_joins != null) {
                return _joins.OfType<QueryJoin>().Select(o => o.ToJSON()).ToList();
            }

            if (!((_source as QueryDataSource)?.ToJSON() is Dictionary<string, object> asObj)) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.MissASforJoin);
            }

            if (_joinType != "CROSS") {
                var onObj = _on as QueryExpression;
                asObj["ON"] = onObj?.ConvertToJSON() ??
                              throw new InvalidOperationException(CouchbaseLiteErrorMessage.MissONforJoin);
            }

            if (_joinType != null) {
                asObj["JOIN"] = _joinType;
            }

            return asObj;
        }

        #endregion

        #region IJoinOn

        public IJoin On([NotNull]IExpression expression)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression);

            _on = expression;
            return this;
        }

        #endregion

        #region IOrderByRouter

        public IOrderBy OrderBy([ItemNotNull]params IOrdering[] orderings)
        {
            CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(orderings), orderings);
            ValidateParams(orderings);
            return new QueryOrderBy(this, orderings);
        }

        #endregion

        #region IWhereRouter
        public IWhere Where([NotNull]IExpression expression)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression);
            return new Where(this, expression);
        }

        #endregion
    }
}

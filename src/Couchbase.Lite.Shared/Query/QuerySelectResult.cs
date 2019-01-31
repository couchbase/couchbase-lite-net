// 
// QuerySelectResult.cs
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

using System;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;
using JetBrains.Annotations;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QuerySelectResult : ISelectResultAs, ISelectResultFrom
    {
        #region Constants

        private const string Tag = nameof(QuerySelectResult);

        #endregion

        internal readonly IExpression Expression;
        private string _alias;
        private string _from = String.Empty;

        internal string ColumnName
        {
            get {
                if (_alias != null) {
                    return _alias;
                }

                QueryTypeExpression keyPathExpr = Expression as QueryTypeExpression;
                var columnName = keyPathExpr?.ColumnName;
                if(columnName == null) {
                    return null;
                }

                return $"{_from}{columnName}".TrimEnd('.');
            }
        }

        public QuerySelectResult(IExpression expression)
        {
            Expression = expression;
        }

        [NotNull]
        public ISelectResult As([NotNull]string alias)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(alias), alias);
            _alias = alias;
            return this;
        }

        [NotNull]
        public ISelectResult From([NotNull]string alias)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(alias), alias);
            _from = $"{alias}.";
            (Expression as QueryTypeExpression).From(alias);
            return this;
        }
    }
}
﻿// 
// Select.cs
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
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class Select : XQuery, ISelect
    {
        #region Constants

        private const string Tag = nameof(Select);

        #endregion

        internal QuerySelectResult[] SelectResults { get; }

        #region Constructors

        public Select(ISelectResult[] selects, bool distinct)
        {
            SelectResults = selects?.OfType<QuerySelectResult>()?.ToArray() ?? new QuerySelectResult[0];

            SelectImpl = this;
            Distinct = distinct;
        }

        #endregion

        #region Public Methods

        public object ToJSON()
        {
            var obj = new List<object?>();
            foreach (var o in SelectResults) {
                obj.Add(o.ToJSON());
            }

            return obj;
        }

        #endregion

        #region IFromRouter

        public IFrom From(IDataSource dataSource)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(dataSource), dataSource);
            return new From(this, dataSource);
        }

        #endregion

        #region IJoinRouter

        public IJoin Join(params IJoin[] joins)
        {
            CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(joins), joins);
            return new QueryJoin(this, joins);
        }

        #endregion
    }
}

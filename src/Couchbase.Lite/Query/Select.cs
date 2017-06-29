// 
// Select.cs
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
using Couchbase.Lite.Query;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class Select : XQuery, ISelect
    {
        #region Variables

        private readonly QueryExpression _select;

        #endregion

        #region Constructors

        public Select(string select, bool distinct)
        {
            if (select != null) {
                _select = new QueryTypeExpression(select);
            }

            SelectImpl = this;
            Distinct = distinct;
        }

        #endregion

        #region Public Methods

        public object ToJSON()
        {
            return _select?.ConvertToJSON();
        }

        #endregion

        #region IFromRouter

        public IFrom From(IDataSource dataSource)
        {
            return new From(this, dataSource);
        }

        #endregion

        #region IJoinRouter

        public IJoin Join(params IJoin[] @join)
        {
            return new Join(this, join);
        }

        #endregion
    }
}

// 
// Having.cs
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
    internal sealed class Having : LimitedQuery, IHaving
    {
        #region Variables

        private readonly IExpression _expression;

        #endregion

        #region Constructors

        internal Having(IExpression expression)
        {
            _expression = expression;
            HavingImpl = this;
        }

        #endregion

        #region Public Methods

        public object ToJSON()
        {
            return (_expression as QueryExpression)?.ConvertToJSON();
        }

        #endregion

        #region IOrderByRouter

        public IOrderBy OrderBy(params IOrderBy[] orderBy)
        {
            return new OrderBy(this, orderBy);
        }

        #endregion
    }
}

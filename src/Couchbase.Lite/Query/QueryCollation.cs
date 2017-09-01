// 
// QueryCollation.cs
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
using System.Globalization;
using Couchbase.Lite.Query;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QueryCollation : QueryExpression, IASCIICollation, IUnicodeCollation
    {
		#region Variables

        private readonly Dictionary<string, object> _collation = new Dictionary<string, object>();
        private List<object> _json;

        #endregion

        #region Constructors

        public QueryCollation(bool unicodeAware)
        {
            if (unicodeAware) {
                _collation["UNICODE"] = true;
				_collation["LOCALE"] = Collation.DefaultLocale;
            }
        }

        #endregion

        public void SetOperand(QueryExpression op)
        {
            _json = new List<object> {"COLLATE", _collation, op.ConvertToJSON()};
        }

        #region Overrides

        protected override object ToJSON()
        {
            return _json;
        }

        #endregion

        #region IASCIICollation

        IASCIICollation IASCIICollation.IgnoreCase(bool ignoreCase)
        {
            _collation["CASE"] = !ignoreCase;
            return this;
        }

        #endregion

        #region IUnicodeCollation

        public IUnicodeCollation IgnoreAccents(bool ignoreAccents)
        {
            _collation["DIAC"] = !ignoreAccents;
            return this;
        }

        IUnicodeCollation IUnicodeCollation.IgnoreCase(bool ignoreCase)
        {
            _collation["CASE"] = !ignoreCase;
            return this;
        }

        public IUnicodeCollation Locale(string localeCode)
        {
            _collation["LOCALE"] = localeCode;
            return this;
        }

        #endregion
    }
}
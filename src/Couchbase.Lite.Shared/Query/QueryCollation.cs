// 
//  QueryCollation.cs
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

using System.Collections.Generic;
using System.Diagnostics;

using Couchbase.Lite.Query;

using JetBrains.Annotations;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QueryCollation : QueryExpression, IASCIICollation, IUnicodeCollation
    {
        #region Variables
        
        [NotNull]private readonly Dictionary<string, object> _collation = new Dictionary<string, object>();
        private List<object> _json;

        #endregion

        #region Constructors

        // Copy constructor.
        public QueryCollation(QueryCollation collationCopy)
        {
            _collation = new Dictionary<string, object>(collationCopy._collation);
        }

        public QueryCollation(bool unicodeAware)
        {
            if (unicodeAware) {
                _collation["UNICODE"] = true;
				_collation["LOCALE"] = Collation.DefaultLocale;
            }
        }

        #endregion

        #region Public Methods

        public void SetOperand([NotNull]QueryExpression op)
        {
            Debug.Assert(op != null);

            _json = new List<object> {"COLLATE", _collation, op.ConvertToJSON()};
        }

        #endregion

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

        public IUnicodeCollation Locale(string locale)
        {
            _collation["LOCALE"] = locale;
            return this;
        }

        #endregion
    }
}
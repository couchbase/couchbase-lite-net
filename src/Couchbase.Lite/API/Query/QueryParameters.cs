// 
//  QueryParameters.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
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

using Newtonsoft.Json;

namespace Couchbase.Lite.Query
{
    public sealed class QueryParameters
    {
        #region Variables

        private readonly Dictionary<string, object> _params = new Dictionary<string, object>();

        #endregion

        #region Public Methods

        public QueryParameters SetBoolean(string name, bool value)
        {
            SetValue(name, value);
            return this;
        }

        public QueryParameters SetDate(string name, DateTimeOffset value)
        {
            SetValue(name, value);
            return this;
        }

        public QueryParameters SetDouble(string name, double value)
        {
            SetValue(name, value);
            return this;
        }

        public QueryParameters SetFloat(string name, float value)
        {
            SetValue(name, value);
            return this;
        }

        public QueryParameters SetInt(string name, int value)
        {
            SetValue(name, value);
            return this;
        }

        public QueryParameters SetLong(string name, long value)
        {
            SetValue(name, value);
            return this;
        }

        public QueryParameters SetString(string name, string value)
        {
            SetValue(name, value);
            return this;
        }

        public QueryParameters SetValue(string name, object value)
        {
            _params[name] = value;
            return this;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return _params != null ? JsonConvert.SerializeObject(_params) : null;
        }

        #endregion
    }
}

// 
// QueryParameters.cs
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

using System;
using System.Collections.Generic;
using Couchbase.Lite.Query;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QueryParameters : IParameters
    {
        private Dictionary<string, object> _stringParams;
        private IList<object> _intParams;

        public void Set(string name, object value)
        {
            if (_intParams != null) {
                throw new InvalidOperationException(
                    "Cannot add string parameters to a query which already has positional ones");
            }

            if (_stringParams == null) {
                _stringParams = new Dictionary<string, object>();
            }

            _stringParams[name] = value;
        }

        public void Set(int index, object value)
        {
            if (_stringParams != null) {
                throw new InvalidOperationException(
                    "Cannot add positional parameters to a query which already has string ones");
            }

            if (_intParams == null) {
                _intParams = new List<object>();
            }

            while (index <= _intParams.Count) {
                _intParams.Add(null);
            }

            _intParams[index] = value;
        }

        public override string ToString()
        {
            if (_stringParams != null) {
                return JsonConvert.SerializeObject(_stringParams);
            }

            return _intParams != null ? JsonConvert.SerializeObject(_intParams) : null;
        }
    }
}

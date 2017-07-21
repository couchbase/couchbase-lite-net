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

using System.Collections.Generic;
using Couchbase.Lite.Query;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QueryParameters : IParameters
    {
        private readonly Dictionary<object, object> _params = new Dictionary<object, object>();

        public IParameters Set(string name, object value)
        {
            _params[name] = value;
            return this;
        }

        public override string ToString()
        {
            return _params != null ? JsonConvert.SerializeObject(_params) : null;
        }
    }
}

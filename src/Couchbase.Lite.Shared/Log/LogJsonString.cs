// 
// LogJsonString.cs
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
using System.Diagnostics;
using System.Linq;
using System.Text;

using Couchbase.Lite.Util;

using JetBrains.Annotations;

using Newtonsoft.Json;

namespace Couchbase.Lite.Logging
{
    internal sealed class LogString
    {
        #region Variables

        private readonly byte[] _unserialized;
        private string _serialized;

        #endregion

        #region Constructors

        public LogString([NotNull]IEnumerable<byte> utf8Bytes)
        {
            Debug.Assert(utf8Bytes != null);
            _unserialized = utf8Bytes.ToArray();
        }

        #endregion

        #region Overrides

        [NotNull]
        public override string ToString()
        {
            return _serialized ?? (_serialized = Encoding.UTF8.GetString(_unserialized));
        }

        #endregion
    }

    internal sealed class LogJsonString
    {
        #region Variables

        private readonly object _unserialized;
        private string _serialized;

        #endregion

        #region Constructors

        public LogJsonString(object obj)
        {
            _unserialized = obj;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return _serialized ?? (_serialized = JsonConvert.SerializeObject(_unserialized));
        }

        #endregion
    }
}


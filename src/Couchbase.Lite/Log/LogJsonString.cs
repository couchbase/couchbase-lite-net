//
// LogJsonString.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Linq;
using System.Text;

namespace Couchbase.Lite.Logging
{
    internal sealed class LogString
    {
        private string _serialized;
        private readonly byte[] _unserialized;

        public LogString(IEnumerable<byte> utf8Bytes)
        {
            _unserialized = utf8Bytes.ToArray();
        }

        public override string ToString()
        {
            if (_serialized == null) {
                _serialized = Encoding.UTF8.GetString(_unserialized);
            }

            return _serialized;
        }
    }

    internal sealed class LogJsonString
    {
        private string _serialized;
        private readonly object _unserialized;

        public LogJsonString(object obj)
        {
            _unserialized = obj;
        }

       /* public override string ToString()
        {
            if (_serialized == null) {
                _serialized = Manager.GetObjectMapper().WriteValueAsString(_unserialized);
            }

            return _serialized;
        }*/
    }
}


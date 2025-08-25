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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

namespace Couchbase.Lite.Logging;

[ExcludeFromCodeCoverage]
internal sealed class LogString
{
    private readonly byte[] _unserialized;
    private string? _serialized;

    public LogString(IEnumerable<byte> utf8Bytes)
    {
        Debug.Assert(utf8Bytes != null);
        _unserialized = utf8Bytes.ToArray();
    }

    public override string ToString()
    {
        return _serialized ??= Encoding.UTF8.GetString(_unserialized);
    }
}

[ExcludeFromCodeCoverage]
internal sealed class LogJsonString(object obj)
{
    private string? _serialized;

    public override string ToString() => _serialized ??= JsonConvert.SerializeObject(obj);
}
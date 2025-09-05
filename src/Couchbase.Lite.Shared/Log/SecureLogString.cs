//
// SecureLogString.cs
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

using System.Linq;
using System.Text;
using System.Text.Json;

namespace Couchbase.Lite.Logging;

internal enum LogMessageSensitivity
{
    PotentiallyInsecure = 1,
    Insecure
}

internal abstract class SecureLogItem(LogMessageSensitivity sensitivity)
{
    protected const int CharLimit = 100;

    // ReSharper disable once UnusedMember.Local
    private readonly LogMessageSensitivity _sensitivity = sensitivity;
}

internal sealed class SecureLogString : SecureLogItem
{
    private readonly byte[]? _bytes;
    private readonly object? _obj;
    private string? _string;

    private string String
    {
        get {
            if (_string != null) {
                return _string;
            }

            string str;
            if (_bytes != null) {
                str = Encoding.UTF8.GetString(_bytes);
            } else if (_obj != null) {
                str = _obj.ToString()!;
            } else {
                str = "(null)";
            }

            _string = str.Length > 100 ? $"{new string(str.Take(CharLimit).ToArray())}..." : str;

            return _string;
        }
    }

    public SecureLogString(string str, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
    {
        _string = str;
    }

    public SecureLogString(byte[] utf8Bytes, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
    {
        _bytes = utf8Bytes;
    }

    public SecureLogString(object obj, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
    {
        _obj = obj;
    }

    public override string ToString() => String;
}

internal sealed class SecureLogJsonString(object input, LogMessageSensitivity sensitivityLevel) : SecureLogItem(sensitivityLevel)
{
    private readonly object? _object = input;
    private string? _str;
        
    private string String 
    {
        get {
            if (_str != null) {
                return _str;
            }

            var str = JsonSerializer.Serialize(_object);
            _str = str.Length > 100 ? $"{new string(str.Take(100).ToArray())}..." : str;

            return _str;
        }
    }

    public override string ToString() => String;
}
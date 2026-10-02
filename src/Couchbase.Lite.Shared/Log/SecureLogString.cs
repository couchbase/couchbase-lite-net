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

using System;
using System.Linq;
using System.Text;

using Couchbase.Lite.Internal.Serialization;

namespace Couchbase.Lite.Logging;

internal enum LogMessageSensitivity
{
    SystemData = 1,
    UserData
}

internal abstract class SecureLogItem(LogMessageSensitivity sensitivity)
{
    private const int CharLimit = 100;

    protected string Process(string input, bool truncate = true)
    {
        var retVal = truncate && input.Length > CharLimit ? $"{new string([.. input.Take(CharLimit)])}..." : input;
        return sensitivity switch {
            LogMessageSensitivity.SystemData => $"<sd>{retVal}</sd>",
            LogMessageSensitivity.UserData => $"<ud>{retVal}</ud>",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

internal sealed class SecureLogString : SecureLogItem
{
    private readonly byte[]? _bytes;
    private readonly object? _obj;
    private readonly Exception? _exception;

    private string String
    {
        get {
            if (field == null) {
                if (_bytes != null) {
                    field = Process(Encoding.UTF8.GetString(_bytes));
                } else if (_exception != null) {
                    // Exceptions are never truncated so that the full message and stack
                    // trace remain available wherever the logs are not redacted
                    field = Process(_exception.ToString(), false);
                } else if (_obj != null) {
                    field = Process(_obj.ToString() ?? "(null)");
                } else {
                    throw new InvalidOperationException("No value provided in SecureLogString");
                }
            }

            return field;
        }
    }

    public SecureLogString(string str, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
    {
        String = Process(str);
    }

    public SecureLogString(byte[] utf8Bytes, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
    {
        _bytes = utf8Bytes;
    }

    public SecureLogString(Exception exception, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
    {
        _exception = exception;
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

    private string String
    {
        get {
            field ??= Process(CouchbaseJson.SerializeLenient(_object));
            return field;
        }
    }

    public override string ToString() => String;
}
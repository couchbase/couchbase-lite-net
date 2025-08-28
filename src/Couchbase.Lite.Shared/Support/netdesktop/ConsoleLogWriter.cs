// 
// ConsoleLogWriter.cs
// 
// Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.DI;
using System.Diagnostics;
using System;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;

#if !__IOS__ && !__ANDROID__

namespace Couchbase.Lite.Support;

[CouchbaseDependency]
internal sealed class ConsoleLogWriter : IConsoleLogWriter
{
    public void Write(LogLevel level, string message)
    {
        try {
            if (Debugger.IsAttached) {
                Debug.WriteLine(message);
            }

            Console.WriteLine(message);
        } catch (ObjectDisposedException) {
            // On WinUI the console can be disposed which means it is no longer 
            // available to write to.  Nothing we can do except ignore.
        }
    }
}

#endif

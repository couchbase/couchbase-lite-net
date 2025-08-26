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

#if __ANDROID__

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Support;

[CouchbaseDependency]
internal sealed class ConsoleLogWriter : IConsoleLogWriter
{
    public void Write(LogLevel level, string message)
    {
        switch (level) {
            case LogLevel.Error:
                global::Android.Util.Log.Error("CouchbaseLite", message);
                break;
            case LogLevel.Warning:
                global::Android.Util.Log.Warn("CouchbaseLite", message);
                break;
            case LogLevel.Info:
                global::Android.Util.Log.Info("CouchbaseLite", message);
                break;
            case LogLevel.Verbose:
            case LogLevel.Debug:
                global::Android.Util.Log.Verbose("CouchbaseLite", message);
                break;
        }
    }
}

#endif

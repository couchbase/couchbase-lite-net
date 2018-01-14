// 
// XunitLogger.cs
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
#if !WINDOWS_UWP
using System;

using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Xunit.Abstractions;

namespace Test.Util
{
    internal sealed class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        public XunitLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public void Log(LogLevel level, string category, string msg)
        {
            try {
                _output.WriteLine($"{level.ToString().ToUpperInvariant()}) {category} {msg}");
            } catch (Exception) {
                // _output is busted, the test is probably already finished.  Nothing we can do
            }
        }
    }
}
#else
using System;

using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.Util
{
    internal sealed class MSTestLogger : ILogger
    {
        #region Variables

        private readonly TestContext _output;

        #endregion

        #region Constructors

        public MSTestLogger(TestContext output)
        {
            _output = output;
        }

        #endregion

        public void Log(LogLevel logLevel, string category, string message)
        {
            _output.WriteLine($"{logLevel.ToString().ToUpperInvariant()}) {category} {message}");
        }
    }
}
#endif
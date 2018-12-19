// 
// XunitLogger.cs
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

using Couchbase.Lite.Logging;

using Xunit.Abstractions;

namespace Test.Util
{
    internal sealed class XunitLogger : ILogger
    {
        #region Variables

        private readonly ITestOutputHelper _output;

        #endregion

        #region Properties

        public LogLevel Level { get; set; }

        #endregion

        #region Constructors

        public XunitLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        #endregion

        #region ILogger

        public void Log(LogLevel level, LogDomain domain, string message)
        {
            if (level < Level) {
                return;
            }

            try {
                _output.WriteLine($"{level.ToString().ToUpperInvariant()}) {domain} {message}");
            } catch (Exception) {
                // _output is busted, the test is probably already finished.  Nothing we can do
            }
        }

        #endregion
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

        public void Log(LogLevel level, LogDomain domain, string message)
        {
            if (level < Level) {
                return;
            }

            _output.WriteLine($"{level.ToString().ToUpperInvariant()}) {domain} {message}");
        }
    }
}
#endif
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
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Test.Util
{
    internal sealed class XunitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;

        public XunitLoggerProvider(ITestOutputHelper output)
        {
            _output = output;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitLogger(_output, categoryName);
        }

        public void Dispose()
        {
            
        }
    }

    internal sealed class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _category;

        public XunitLogger(ITestOutputHelper output, string categoryName)
        {
            _output = output;
            _category = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var finalStr = formatter(state, exception);
            _output.WriteLine($"{logLevel.ToString().ToUpperInvariant()}) {_category} {finalStr}");
        }
    }
}
#else
using System;
using Couchbase.Lite.Support;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.Util
{
    internal sealed class MSTestLoggerProvider : ILoggerProvider
    {
        private readonly TestContext _output;

        public MSTestLoggerProvider(TestContext output)
        {
            _output = output;
        }

        public void Dispose()
        {
            
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new MSTestLogger(categoryName, _output);
        }
    }

    internal sealed class MSTestLogger : ILogger
    {
        #region Variables

        private readonly TestContext _output;
        private readonly string _category;

        #endregion

        #region Constructors

        public MSTestLogger(string categoryName, TestContext output)
        {
            _category = categoryName;
            _output = output;
        }

        #endregion

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var finalStr = formatter(state, exception);
            _output.WriteLine($"{logLevel.ToString().ToUpperInvariant()}) {_category} {finalStr}");
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }
    }
}
#endif
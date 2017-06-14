// 
// AndroidDefaultLogger.cs
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
using System;
using Microsoft.Extensions.Logging;

namespace Couchbase.Lite.Support
{
    internal sealed class AndroidLoggerProvider : ILoggerProvider
    {
        #region IDisposable

        public void Dispose()
        {
            
        }

        #endregion

        #region ILoggerProvider

        public ILogger CreateLogger(string categoryName)
        {
            return new AndroidDefaultLogger(categoryName);
        }

        #endregion
    }

    internal sealed class AndroidDefaultLogger : ILogger
    {
        #region Variables

        private readonly string _category;

        #endregion

        #region Constructors

        public AndroidDefaultLogger(string categoryName)
        {
            _category = categoryName;
        }

        #endregion

        #region Private Methods

        private string MakeMessage(string msg)
        {
            var dateTime = DateTime.Now.ToLocalTime().ToString("yyyy-M-d hh:mm:ss.fffK");
            return $"[{Environment.CurrentManagedThreadId}] {dateTime} {msg}";
        }

        #endregion

        #region ILogger

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
            var finalStr = MakeMessage($"{_category} {formatter(state, exception)}");
            switch (logLevel) {
                case LogLevel.Critical:
                case LogLevel.Error:
                    global::Android.Util.Log.Error("CouchbaseLite", finalStr);
                    break;
                case LogLevel.Warning:
                    global::Android.Util.Log.Warn("CouchbaseLite", finalStr);
                    break;
                case LogLevel.Information:
                    global::Android.Util.Log.Info("CouchbaseLite", finalStr);
                    break;
                case LogLevel.Debug:
                case LogLevel.Trace:
                    global::Android.Util.Log.Verbose("CouchbaseLite", finalStr);
                    break;
            }
        }

        #endregion
    }
}

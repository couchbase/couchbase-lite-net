// 
// FileLoggerProvider.cs
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
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Couchbase.Lite.Support
{
    internal sealed class FileLoggerProvider : ILoggerProvider
    {
        #region Variables

        private readonly string _logDirectory;
        private readonly string _filename = $"Log-{GetTimeStamp()}.txt";

        #endregion

        #region Constructors

        public FileLoggerProvider(string logDirectory)
        {
            _logDirectory = logDirectory;
        }

        #endregion

        #region Private Methods

        private static string GetTimeStamp()
        {
            var now = DateTime.Now;
            return $"{now.Year:D4}{now.Month:D2}{now.Day:D2}-{now.Hour:D2}{now.Minute:D2}{now.Second:D2}{now.Millisecond:D3}";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
        }

        #endregion

        #region ILoggerProvider

        public ILogger CreateLogger(string categoryName)
        {
            var directory = _logDirectory ?? Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(directory);
            return new FileLogger(categoryName, Path.Combine(_logDirectory, _filename));
        }

        #endregion
    }

    /// <summary>
    /// A default logging implementation that has a virtual method for doing the actual
    /// log writing (useful as a base class to get preformatted messages for a custom logger)
    /// </summary>
    internal sealed class FileLogger : ILogger, IDisposable
    {
        #region Constants

        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

        #endregion

        #region Variables

        private readonly string _category;
        private StreamWriter _writer;

        #endregion

        #region Constructors

        internal FileLogger(string category, string filePath)
        {
            _writer = new StreamWriter(File.Open(filePath, FileMode.Create,
                FileAccess.Write, FileShare.ReadWrite)) {
                AutoFlush = true
            };
            _category = category;
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
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

        public async void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            await Semaphore.WaitAsync();
            try {
                var finalStr = formatter(state, exception);
                _writer?.WriteLine($"{logLevel.ToString().ToUpperInvariant()}) {_category} {finalStr}");
            } finally {
                Semaphore.Release();
            }
        }

        #endregion
    }
}

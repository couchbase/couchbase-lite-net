// 
// UwpDefaultLogger.cs
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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Windows.Storage;

namespace Couchbase.Lite.Support
{
    internal sealed class UwpLoggerProvider : ILoggerProvider
    {
        #region Constants

        public static readonly string Filename = $"Log-{GetTimeStamp()}.txt";

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
            return new UwpDefaultLogger(categoryName, Filename);
        }

        #endregion
    }

    internal sealed class UwpDefaultLogger : ILogger, IDisposable
    {
        #region Constants

        private static readonly string LogPath;
        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

        #endregion

        #region Variables

        private readonly string _category;
        private bool _disposed;
        private StreamWriter _writer;

        #endregion

        #region Constructors

        static UwpDefaultLogger()
        {
            var localFolderPath = ApplicationData.Current.LocalFolder.Path;
            LogPath = Path.Combine(localFolderPath, "Logs");
            if (!Directory.Exists(LogPath)) {
                Directory.CreateDirectory(LogPath);
            }
        }

        public UwpDefaultLogger(string categoryName, string filename)
        {
            _category = categoryName;
            Open(filename);
        }

        #endregion

        #region Private Methods

        private void Open(string filename)
        {
            _writer = new StreamWriter(File.Open(Path.Combine(LogPath, filename), FileMode.Create,
            FileAccess.Write, FileShare.ReadWrite)) {
                AutoFlush = true
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if(_disposed) {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
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
            if (_disposed) {
                return;
            }
            
            await Semaphore.WaitAsync().ConfigureAwait(false);
            try {
                var finalStr = formatter(state, exception);
                _writer.WriteLine($"{_category} {finalStr}");
            } finally {
                Semaphore.Release();
            }
        }

        #endregion
    }
}

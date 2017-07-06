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
        public readonly string _filename = $"Log-{GetTimeStamp()}.txt";


        private static string GetTimeStamp()
        {
            var now = DateTime.Now;
            return $"{now.Year:D4}{now.Month:D2}{now.Day:D2}-{now.Hour:D2}{now.Minute:D2}{now.Second:D2}{now.Millisecond:D3}";
        }

        #region IDisposable

        public void Dispose()
        {
            
        }

        #endregion

        #region ILoggerProvider

        public ILogger CreateLogger(string categoryName)
        {
            return new UwpDefaultLogger(categoryName, _filename);
        }

        #endregion
    }

    internal sealed class UwpDefaultLogger : ILogger, IDisposable
    {
        #region Variables

        private readonly string _category;
        private readonly ManualResetEventSlim _loggingReady = new ManualResetEventSlim();
        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        private bool _disposed;
        private StreamWriter _writer;

        #endregion

        #region Constructors

        public UwpDefaultLogger(string categoryName, string filename)
        {
            _category = categoryName;
            OpenAsync(filename);
        }

        #endregion

        private async Task OpenAsync(string filename)
        {
            var result = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Logs", CreationCollisionOption.OpenIfExists);
            if (_disposed) {
                _loggingReady.Set();
                return;
            }

            _writer = new StreamWriter(File.Open(Path.Combine(result.Path, filename), FileMode.Create,
                FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };
            _loggingReady.Set();
        }

        #region IDisposable

        public void Dispose()
        {
            if(_disposed) {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _loggingReady.Dispose();
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

            _loggingReady.Wait();
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

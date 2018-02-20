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

using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;

using JetBrains.Annotations;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// A default logging implementation that has a virtual method for doing the actual
    /// log writing (useful as a base class to get preformatted messages for a custom logger)
    /// </summary>
    internal sealed class FileLogger : ILogger, IDisposable
    {
        #region Constants

        [NotNull]private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

        #endregion

        #region Variables
        
        private StreamWriter _writer;

        #endregion

        #region Constructors

        internal FileLogger([NotNull]string filePath)
        {
            Directory.CreateDirectory(filePath);
            var logFileName = $"TextLog-{DateTimeOffset.Now.ToUnixTimeSeconds()}";
            _writer = new StreamWriter(File.Open(Path.Combine(filePath, logFileName), FileMode.Create,
                FileAccess.Write, FileShare.ReadWrite)) {
                AutoFlush = true
            };
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

        public async void Log(LogLevel logLevel, string category, string message)
        {
            await Semaphore.WaitAsync();
            try {
                _writer?.WriteLine($"{logLevel.ToString().ToUpperInvariant()}) {category} {message}");
            } finally {
                Semaphore.Release();
            }
        }

        #endregion
    }
}

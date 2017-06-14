//
//  DefaultLogger.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Couchbase.Lite.Support
{
    internal sealed class UwpDefaultLogger : DefaultLogger
    {
        private readonly ManualResetEventSlim _loggingReady = new ManualResetEventSlim();
        private StreamWriter _writer;
        private bool _disposed;

        public UwpDefaultLogger() : base(false)
        {
            var filename = $"Log-{GetTimeStamp()}.txt";
            ApplicationData.Current.LocalFolder.CreateFolderAsync("Logs", CreationCollisionOption.OpenIfExists)
                .AsTask().ContinueWith(t => {
                    if(_disposed) {
                        return;
                    }

                    _writer = new StreamWriter(File.Open(Path.Combine(t.Result.Path, filename), FileMode.Create, FileAccess.Write, FileShare.Read));
                    _writer.AutoFlush = true;
                    _loggingReady.Set();
                });
        }

        private string GetTimeStamp()
        {
            var now = DateTime.Now;
            return $"{now.Year:D4}{now.Month:D2}{now.Day:D2}-{now.Hour:D2}{now.Minute:D2}{now.Second:D2}{now.Millisecond:D3}";
        }

        protected override void PerformWrite(string final)
        {
            if(_disposed) {
                return;
            }

            _loggingReady.Wait();
            _writer.WriteLine(final);
        }

        protected override void Dispose(bool disposing)
        {
            if(_disposed) {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _loggingReady.Dispose();
        }
    }
}

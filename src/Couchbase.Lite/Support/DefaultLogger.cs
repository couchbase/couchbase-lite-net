//
//  DefaultLogger.cs
//
//  Author:
//      Jim Borden  <jim.borden@couchbase.com>
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
using System.IO;
using System.Threading.Tasks;
using Couchbase.Lite.DI;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// A default logging implementation that has a virtual method for doing the actual
    /// log writing (useful as a base class to get preformatted messages for a custom logger)
    /// </summary>
    public class DefaultLogger : ILogger, IDisposable
    {
        #region Variables

        private StreamWriter _writer;

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor, creates a file to log to.  Warning:
        /// This is not allowed on some systems
        /// </summary>
        public DefaultLogger() : this(true)
        {
            
        }

        /// <summary>
        /// Constructor which optionally creates a file to log to
        /// </summary>
        /// <param name="createWriter">If <c>true</c>, create a file in the "Logs" directory
        /// next to the location of the executable that is running</param>
        protected DefaultLogger(bool createWriter)
        {
            if(!createWriter) {
                return;
            }

            var directory = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(directory);
            _writer = new StreamWriter(File.Open(Path.Combine(directory, $"Log-{GetTimeStamp()}.txt"), FileMode.Create,
                FileAccess.Write, FileShare.Read)) {
                AutoFlush = true
            };
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Used to free resources
        /// </summary>
        /// <param name="disposing">If <c>true</c>, <see cref="IDisposable.Dispose"/> is being called,
        /// otherwise this object is being finalized.</param>
        protected virtual void Dispose(bool disposing)
        {
            _writer?.Dispose();
            _writer = null;
        }

        /// <summary>
        /// Writes the final formatted string to whatever mechanism this logger
        /// chooses (to a file by default)
        /// </summary>
        /// <param name="final">The formatted string to write</param>
        protected virtual void PerformWrite(string final)
        {
            _writer.WriteLine(final);
        }

        #endregion

        #region Private Methods

        private string GetTimeStamp()
        {
            var now = DateTime.Now;
            return $"{now.Year:D4}{now.Month:D2}{now.Day:D2}-{now.Hour:D2}{now.Minute:D2}{now.Second:D2}{now.Millisecond:D3}";
        }

        private string MakeLine(string level, string tag, string msg)
        {
            return $"[{level}] {tag} {MakeMessage(msg)}";
        }

        private string MakeLine(string level, string tag, string msg, Exception tr)
        {
            return $"{level} {tag} {MakeMessage(msg, tr)}";
        }

        private string MakeMessage(string msg, Exception tr)
        {
            var dateTime = DateTime.Now.ToLocalTime().ToString("yyyy-M-d hh:mm:ss.fffK");
            return $"[{Environment.CurrentManagedThreadId}] {dateTime} {msg}:\r\n{tr}";
        }

        private string MakeMessage(string msg)
        {
            var dateTime = DateTime.Now.ToLocalTime().ToString("yyyy-M-d hh:mm:ss.fffK");
            return $"[{Environment.CurrentManagedThreadId}] {dateTime} {msg}";
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region ILogger

        /// <inheritdoc />
        public void D(string tag, string msg)
        {
            var line = MakeLine("DEBUG", tag, msg);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void D(string tag, string format, params object[] args)
        {
            var line = MakeLine("DEBUG", tag, String.Format(format, args));
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void D(string tag, string msg, Exception tr)
        {
            var line = MakeLine("DEBUG", tag, msg, tr);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void E(string tag, string msg)
        {
            var line = MakeLine("ERROR", tag, msg);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void E(string tag, string format, params object[] args)
        {
            var line = MakeLine("ERROR", tag, String.Format(format, args));
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void E(string tag, string msg, Exception tr)
        {
            var line = MakeLine("ERROR", tag, msg, tr);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void I(string tag, string msg)
        {
            var line = MakeLine("INFO", tag, msg);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void I(string tag, string format, params object[] args)
        {
            var line = MakeLine("INFO", tag, String.Format(format, args));
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void I(string tag, string msg, Exception tr)
        {
            var line = MakeLine("INFO", tag, msg, tr);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void V(string tag, string msg)
        {
            var line = MakeLine("VERBOSE", tag, msg);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void V(string tag, string format, params object[] args)
        {
            var line = MakeLine("VERBOSE", tag, String.Format(format, args));
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void V(string tag, string msg, Exception tr)
        {
            var line = MakeLine("VERBOSE", tag, msg, tr);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void W(string tag, string msg)
        {
            var line = MakeLine("WARN", tag, msg);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void W(string tag, string format, params object[] args)
        {
            var line = MakeLine("WARN", tag, String.Format(format, args));
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        /// <inheritdoc />
        public void W(string tag, string msg, Exception tr)
        {
            var line = MakeLine("WARN", tag, msg, tr);
            Task.Factory.StartNew(() => PerformWrite(line));
        }

        #endregion
    }
}

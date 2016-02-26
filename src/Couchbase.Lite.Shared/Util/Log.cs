//
// Log.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Couchbase.Lite.Util
{
    /// <summary>
    /// Centralized logging facility.
    /// </summary>
    public static class Log
    { 

        #region Enums

        /// <summary>
        /// A level of logging verbosity
        /// </summary>
        public enum LogLevel
        {
            /// <summary>
            /// No logs are output
            /// </summary>
            None,

            /// <summary>
            /// Informational logs are output (the default for most)
            /// </summary>
            Base,

            /// <summary>
            /// Verbose logs are output
            /// </summary>
            Verbose,

            /// <summary>
            /// Debugging logs are output (Only applicable in debug builds)
            /// </summary>
            Debug
        }

        #endregion

        #region Variables

        internal static readonly LogTo To = new LogTo();

        /// <summary>
        /// The available logging domains (for use with setting the
        /// logging level on various domains)
        /// </summary>
        public static readonly LogDomains Domains = new LogDomains(To);

        internal static LogScrubSensitivity ScrubSensitivity { get; set; }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the logging level for Log.* calls (domains
        /// must be set with SetLevelForDomain or SetDomainLevels
        /// </summary>
        public static LogLevel Level 
        {
            get { return To.All.Level; }
            set { To.All.Level = value; }
        }

        private static object logger = LoggerFactory.CreateLogger();
        internal static ILogger Logger { 
            get { return (ILogger)logger; }
        }

        #endregion

        #region Constructors

        static Log()
        {
            Level = LogLevel.Base;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the logger.
        /// </summary>
        /// <returns><c>true</c>, if Logger was set, <c>false</c> otherwise.</returns>
        /// <param name="customLogger">Custom logger.</param>
        public static bool SetLogger(ILogger customLogger)
        {
            var currentLogger = Logger;
            Interlocked.CompareExchange(ref logger, customLogger, currentLogger);
            return Logger == customLogger;
        }

        /// <summary>
        /// Sets the logger to the library provided logger
        /// </summary>
        /// <returns><c>true</c>, if logger was set, <c>false</c> otherwise.</returns>
        public static bool SetDefaultLogger()
        {
            return SetLogger(LoggerFactory.CreateLogger());
        }

        /// <summary>
        /// Sets up Couchbase Lite to use the default logger (an internal class),
        /// with the specified logging level
        /// </summary>
        /// <returns><c>true</c>, if the logger was changed, <c>false</c> otherwise.</returns>
        /// <param name="level">The levels to log</param>
        [Obsolete("Use the SetDefaultLogger() with no arguments to restore the default logger," +
            " and use the Level property to change the verbosity")]
        public static bool SetDefaultLoggerWithLevel(SourceLevels level)
        {
            if (level.HasFlag(SourceLevels.All)) {
                Level = LogLevel.Debug;
            } else {
                if (level.HasFlag(SourceLevels.Information) ||
                    level.HasFlag(SourceLevels.Warning) ||
                    level.HasFlag(SourceLevels.Error)) {
                    Level = LogLevel.Base;
                }
                if (level.HasFlag(SourceLevels.Verbose)) {
                    Level = LogLevel.Verbose;
                }
                if (level.HasFlag(SourceLevels.ActivityTracing)) {
                    Level = LogLevel.Debug;
                }
            }

            return SetLogger(LoggerFactory.CreateLogger());
        }

        /// <summary>Send a VERBOSE message.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        public static void V(string tag, string msg)
        {
            To.All.V(tag, msg);
        }

        /// <summary>Send a VERBOSE message and log the exception.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        /// <param name="tr">An exception to log</param>
        public static void V(string tag, string msg, Exception tr)
        {
            To.All.V(tag, msg, tr);
        }

        /// <summary>Send a VERBOSE message</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="format">The format of the message you would like logged.</param>
        /// <param name="args">The message format arguments</param>
        public static void V(string tag, string format, params object[] args)
        {
            To.All.V(tag, format, args);
        }

        /// <summary>Send a DEBUG message.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void D(string tag, string msg)
        {
            To.All.D(tag, msg);
        }

        /// <summary>Send a DEBUG message and log the exception.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        /// <param name="tr">An exception to log</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void D(string tag, string msg, Exception tr)
        {
            To.All.D(tag, msg, tr);
        }

        /// <summary>Send a DEBUG message</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="format">The format of the message you would like logged.</param>
        /// <param name="args">The message format arguments</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void D(string tag, string format, params object[] args)
        {
            To.All.D(tag, format, args);
        }

        /// <summary>Send an INFO message.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        public static void I(string tag, string msg)
        {
            To.All.I(tag, msg);
        }

        /// <summary>Send a INFO message and log the exception.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        /// <param name="tr">An exception to log</param>
        public static void I(string tag, string msg, Exception tr)
        {
            To.All.I(tag, msg, tr);
        }

        /// <summary>Send a INFO message</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="format">The format of the message you would like logged.</param>
        /// <param name="args">The message format arguments</param>
        public static void I(string tag, string format, params object[] args)
        {
            To.All.I(tag, format, args);
        }

        /// <summary>Send a WARN message.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        public static void W(string tag, string msg)
        {
            To.All.W(tag, msg);
        }

        /// <summary>Send a WARN message.</summary>
        /// <param name="tag">Tag.</param>
        /// <param name="tr">Exception</param>
        [Obsolete("This method signature is not like the others and will be removed")]
        public static void W(string tag, Exception tr)
        {
            To.All.W(tag, "No message, do not call this method");
        }

        /// <summary>Send a WARN message and log the exception.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        /// <param name="tr">An exception to log</param>
        public static void W(string tag, string msg, Exception tr)
        {
            To.All.W(tag, msg, tr);
        }

        /// <summary>Send a WARN message and log the exception.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="format">The format of the message you would like logged.</param>
        /// <param name="args">The message format arguments</param>
        public static void W(string tag, string format, params object[] args)
        {
            To.All.I(tag, format, args);
        }

        /// <summary>Send an ERROR message.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        public static void E(string tag, string msg)
        {
            To.All.E(tag, msg);
        }

        /// <summary>Send a ERROR message and log the exception.</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        /// <param name="tr">An exception to log</param>
        public static void E(string tag, string msg, Exception tr)
        {
            To.All.E(tag, msg, tr);
        }

        /// <summary>Send a ERROR message</summary>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="format">The format of the message you would like logged.</param>
        /// <param name="args">The message format arguments</param>
        public static void E(string tag, string format, params object[] args)
        {
            To.All.E(tag, format, args);
        }

        #endregion

    }
}

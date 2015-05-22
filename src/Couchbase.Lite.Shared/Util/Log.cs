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
using Couchbase.Lite.Util;
using System.Threading;
using System.Diagnostics;

namespace Couchbase.Lite.Util
{
    /// <summary>
    /// Centralized logging facility.
    /// </summary>
    public static class Log
    {
        private static object logger = LoggerFactory.CreateLogger();
        private static ILogger Logger { 
            get { return (ILogger)logger; }
        }

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
        /// Sets up Couchbase Lite to use the default logger (an internal class),
        /// with the specified logging level
        /// </summary>
        /// <returns><c>true</c>, if the logger was changed, <c>false</c> otherwise.</returns>
        /// <param name="level">The levels to log</param>
        public static bool SetDefaultLoggerWithLevel(SourceLevels level)
        {
            return SetLogger(new CustomLogger(level));
        }

        /// <summary>Send a VERBOSE message.</summary>
        /// <remarks>Send a VERBOSE message.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        [System.Diagnostics.Conditional("TRACE")]
        public static void V(string tag, string msg)
        {
            if (Logger != null)
            {
                Logger.V(tag, msg);
            }
        }

        /// <summary>Send a VERBOSE message and log the exception.</summary>
        /// <remarks>Send a VERBOSE message and log the exception.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        /// <param name="tr">An exception to log</param>
        [System.Diagnostics.Conditional("TRACE")]
        public static void V(string tag, string msg, Exception tr)
        {
            if (Logger != null)
            {
                Logger.V(tag, msg, tr);
            }
        }

        /// <summary>Send a VERBOSE message and log the exception.</summary>
        /// <remarks>Send a VERBOSE message and log the exception.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="format">The message you would like logged.</param>
        /// <param name="args">string format arguments</param>
        [System.Diagnostics.Conditional("TRACE")]
        public static void V(string tag, string format, params object[] args)
        {
            if (Logger != null)
            {
                Logger.V(tag, format, args);
            }
        }

        /// <summary>Send a DEBUG message.</summary>
        /// <remarks>Send a DEBUG message.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void D(string tag, string msg)
        {
            if (Logger != null)
            {
                Logger.D(tag, msg);
            }
        }

        /// <summary>Send a DEBUG message and log the exception.</summary>
        /// <remarks>Send a DEBUG message and log the exception.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        /// <param name="tr">An exception to log</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void D(string tag, string msg, Exception tr)
        {
            if (Logger != null)
            {
                Logger.D(tag, msg, tr);
            }
        }

        /// <summary>Send a DEBUG message and log the exception.</summary>
        /// <remarks>Send a DEBUG message and log the exception.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="format">The message you would like logged.</param>
        /// <param name="args">string format arguments</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void D(string tag, string  format, params object[] args)
        {
            if (Logger != null)
            {
                Logger.D(tag, format, args);
            }
        }

        /// <summary>Send an INFO message.</summary>
        /// <remarks>Send an INFO message.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        public static void I(string tag, string msg)
        {
            if (Logger != null)
            {
                Logger.I(tag, msg);
            }
        }

        /// <summary>Send a INFO message and log the exception.</summary>
        /// <remarks>Send a INFO message and log the exception.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        /// <param name="tr">An exception to log</param>
        public static void I(string tag, string msg, Exception tr)
        {
            if (Logger != null)
            {
                Logger.I(tag, msg, tr);
            }
        }

        /// <summary>Send a INFO message and log the exception.</summary>
        /// <remarks>Send a INFO message and log the exception.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="format">The message you would like logged.</param>
        /// <param name="args">string format arguments</param>
        public static void I(string tag, string  format, params object[] args)
        {
            if (Logger != null)
            {
                Logger.I(tag, format, args);
            }
        }

        /// <summary>Send a WARN message.</summary>
        /// <remarks>Send a WARN message.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        public static void W(string tag, string msg)
        {
            if (Logger != null)
            {
                Logger.W(tag, msg);
            }
        }

        /// <summary>Send a WARN message.</summary>
        /// <remarks>Send a WARN message.</remarks>
        /// <param name="tag">Tag.</param>
        /// <param name="tr">Exception</param>
        public static void W(string tag, Exception tr)
        {
            if (Logger != null)
            {
                Logger.W(tag, tr);
            }
        }

        /// <summary>Send a WARN message and log the exception.</summary>
        /// <remarks>Send a WARN message and log the exception.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        /// <param name="tr">An exception to log</param>
        public static void W(string tag, string msg, Exception tr)
        {
            if (Logger != null)
            {
                Logger.W(tag, msg, tr);
            }
        }

        /// <summary>Send a WARN message and log the exception.</summary>
        /// <remarks>Send a WARN message and log the exception.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="format">The message you would like logged.</param>
        /// <param name="args">string format arguments</param>
        public static void W(string tag, string  format, params object[] args)
        {
            if (Logger != null)
            {
                Logger.W(tag, format, args);
            }
        }

        /// <summary>Send an ERROR message.</summary>
        /// <remarks>Send an ERROR message.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        public static void E(string tag, string msg)
        {
            if (Logger != null)
            {
                Logger.E(tag, msg);
            }
        }

        /// <summary>Send a ERROR message and log the exception.</summary>
        /// <remarks>Send a ERROR message and log the exception.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="msg">The message you would like logged.</param>
        /// <param name="tr">An exception to log</param>
        public static void E(string tag, string msg, Exception tr)
        {
            if (Logger != null)
            {
                Logger.E(tag, msg, tr);
            }
        }

        /// <summary>Send a ERROR message and log the exception.</summary>
        /// <remarks>Send a ERROR message and log the exception.</remarks>
        /// <param name="tag">
        /// Used to identify the source of a log message.  It usually identifies
        /// the class or activity where the log call occurs.
        /// </param>
        /// <param name="format">The message you would like logged.</param>
        /// <param name="args">string format arguments</param>
        public static void E(string tag, string format, params object[] args)
        {
            if (Logger != null)
            {
                Logger.E(tag, format, args);
            }
        }
    }
}

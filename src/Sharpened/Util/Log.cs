// 
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
//using System;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Util
{
	public class Log
	{
		private static Logger logger = LoggerFactory.CreateLogger();

		/// <summary>A map of tags and their enabled log level</summary>
		private static ConcurrentHashMap<string, int> enabledTags;

		/// <summary>Logging tags</summary>
		public const string Tag = "CBLite";

		public const string TagSync = "Sync";

		public const string TagRemoteRequest = "RemoteRequest";

		public const string TagView = "View";

		public const string TagQuery = "Query";

		public const string TagChangeTracker = "ChangeTracker";

		public const string TagRouter = "Router";

		public const string TagDatabase = "Database";

		public const string TagListener = "Listener";

		public const string TagMultiStreamWriter = "MultistreamWriter";

		public const string TagBlobStore = "BlobStore";

		/// <summary>Logging levels -- values match up with android.util.Log</summary>
		public const int Verbose = 2;

		public const int Debug = 3;

		public const int Info = 4;

		public const int Warn = 5;

		public const int Error = 6;

		public const int Assert = 7;

		static Log()
		{
			// default "catch-all" tag
			enabledTags = new ConcurrentHashMap<string, int>();
			enabledTags.Put(Log.Tag, Warn);
			enabledTags.Put(Log.TagSync, Warn);
			enabledTags.Put(Log.TagRemoteRequest, Warn);
			enabledTags.Put(Log.TagView, Warn);
			enabledTags.Put(Log.TagQuery, Warn);
			enabledTags.Put(Log.TagChangeTracker, Warn);
			enabledTags.Put(Log.TagRouter, Warn);
			enabledTags.Put(Log.TagDatabase, Warn);
			enabledTags.Put(Log.TagListener, Warn);
			enabledTags.Put(Log.TagMultiStreamWriter, Warn);
			enabledTags.Put(Log.TagBlobStore, Warn);
		}

		/// <summary>Enable logging for a particular tag / loglevel combo</summary>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="logLevel">
		/// The loglevel to enable.  Anything matching this loglevel
		/// or having a more urgent loglevel will be emitted.  Eg, Log.VERBOSE.
		/// </param>
		public static void EnableLogging(string tag, int logLevel)
		{
			enabledTags.Put(tag, logLevel);
		}

		/// <summary>Is logging enabled for given tag / loglevel combo?</summary>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="logLevel">
		/// The loglevel to check whether it's enabled.  Will match this loglevel
		/// or a more urgent loglevel.  Eg, if Log.ERROR is enabled and Log.VERBOSE
		/// is passed as a paremeter, it will return true.
		/// </param>
		/// <returns>boolean indicating whether logging is enabled.</returns>
		internal static bool IsLoggingEnabled(string tag, int logLevel)
		{
			// this hashmap lookup might be a little expensive, and so it might make
			// sense to convert this over to a CopyOnWriteArrayList
			if (enabledTags.ContainsKey(tag))
			{
				int logLevelForTag = enabledTags.Get(tag);
				return logLevel >= logLevelForTag;
			}
			return false;
		}

		/// <summary>Send a VERBOSE message.</summary>
		/// <remarks>Send a VERBOSE message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		public static void V(string tag, string msg)
		{
			if (logger != null)
			{
				logger.V(tag, msg);
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
		public static void V(string tag, string msg, Exception tr)
		{
			if (logger != null)
			{
				logger.V(tag, msg, tr);
			}
		}

		/// <summary>Send a VERBOSE message.</summary>
		/// <remarks>Send a VERBOSE message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="formatString">The string you would like logged plus format specifiers.
		/// 	</param>
		/// <param name="args">Variable number of Object args to be used as params to formatString.
		/// 	</param>
		public static void V(string tag, string formatString, params object[] args)
		{
			if (logger != null && IsLoggingEnabled(tag, Verbose))
			{
				try
				{
					logger.V(tag, string.Format(formatString, args));
				}
				catch (Exception e)
				{
					logger.V(tag, string.Format("Unable to format log: %s", formatString), e);
				}
			}
		}

		/// <summary>Send a VERBOSE message and log the exception.</summary>
		/// <remarks>Send a VERBOSE message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="formatString">The string you would like logged plus format specifiers.
		/// 	</param>
		/// <param name="tr">An exception to log</param>
		/// <param name="args">Variable number of Object args to be used as params to formatString.
		/// 	</param>
		public static void V(string tag, string formatString, Exception tr, params object
			[] args)
		{
			if (logger != null && IsLoggingEnabled(tag, Verbose))
			{
				try
				{
					logger.V(tag, string.Format(formatString, args), tr);
				}
				catch (Exception e)
				{
					logger.V(tag, string.Format("Unable to format log: %s", formatString), e);
				}
			}
		}

		/// <summary>Send a DEBUG message.</summary>
		/// <remarks>Send a DEBUG message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		public static void D(string tag, string msg)
		{
			if (logger != null)
			{
				logger.D(tag, msg);
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
		public static void D(string tag, string msg, Exception tr)
		{
			if (logger != null)
			{
				logger.D(tag, msg, tr);
			}
		}

		/// <summary>Send a DEBUG message.</summary>
		/// <remarks>Send a DEBUG message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="formatString">The string you would like logged plus format specifiers.
		/// 	</param>
		/// <param name="args">Variable number of Object args to be used as params to formatString.
		/// 	</param>
		public static void D(string tag, string formatString, params object[] args)
		{
			if (logger != null && IsLoggingEnabled(tag, Debug))
			{
				try
				{
					logger.D(tag, string.Format(formatString, args));
				}
				catch (Exception e)
				{
					logger.D(tag, string.Format("Unable to format log: %s", formatString), e);
				}
			}
		}

		/// <summary>Send a DEBUG message and log the exception.</summary>
		/// <remarks>Send a DEBUG message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="formatString">The string you would like logged plus format specifiers.
		/// 	</param>
		/// <param name="tr">An exception to log</param>
		/// <param name="args">Variable number of Object args to be used as params to formatString.
		/// 	</param>
		public static void D(string tag, string formatString, Exception tr, params object
			[] args)
		{
			if (logger != null && IsLoggingEnabled(tag, Debug))
			{
				try
				{
					logger.D(tag, string.Format(formatString, args, tr));
				}
				catch (Exception e)
				{
					logger.D(tag, string.Format("Unable to format log: %s", formatString), e);
				}
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
			if (logger != null)
			{
				logger.I(tag, msg);
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
			if (logger != null)
			{
				logger.I(tag, msg, tr);
			}
		}

		/// <summary>Send an INFO message.</summary>
		/// <remarks>Send an INFO message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="formatString">The string you would like logged plus format specifiers.
		/// 	</param>
		/// <param name="args">Variable number of Object args to be used as params to formatString.
		/// 	</param>
		public static void I(string tag, string formatString, params object[] args)
		{
			if (logger != null && IsLoggingEnabled(tag, Info))
			{
				try
				{
					logger.I(tag, string.Format(formatString, args));
				}
				catch (Exception e)
				{
					logger.I(tag, string.Format("Unable to format log: %s", formatString), e);
				}
			}
		}

		/// <summary>Send a INFO message and log the exception.</summary>
		/// <remarks>Send a INFO message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="formatString">The string you would like logged plus format specifiers.
		/// 	</param>
		/// <param name="tr">An exception to log</param>
		/// <param name="args">Variable number of Object args to be used as params to formatString.
		/// 	</param>
		public static void I(string tag, string formatString, Exception tr, params object
			[] args)
		{
			if (logger != null && IsLoggingEnabled(tag, Info))
			{
				try
				{
					logger.I(tag, string.Format(formatString, args, tr));
				}
				catch (Exception e)
				{
					logger.I(tag, string.Format("Unable to format log: %s", formatString), e);
				}
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
			if (logger != null)
			{
				logger.W(tag, msg);
			}
		}

		/// <summary>Send a WARN message and log the exception.</summary>
		/// <remarks>Send a WARN message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="tr">An exception to log</param>
		public static void W(string tag, Exception tr)
		{
			if (logger != null)
			{
				logger.W(tag, tr);
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
			if (logger != null)
			{
				logger.W(tag, msg, tr);
			}
		}

		/// <summary>Send a WARN message.</summary>
		/// <remarks>Send a WARN message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="formatString">The string you would like logged plus format specifiers.
		/// 	</param>
		/// <param name="args">Variable number of Object args to be used as params to formatString.
		/// 	</param>
		public static void W(string tag, string formatString, params object[] args)
		{
			if (logger != null && IsLoggingEnabled(tag, Warn))
			{
				try
				{
					logger.W(tag, string.Format(formatString, args));
				}
				catch (Exception e)
				{
					logger.W(tag, string.Format("Unable to format log: %s", formatString), e);
				}
			}
		}

		/// <summary>Send a WARN message and log the exception.</summary>
		/// <remarks>Send a WARN message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="formatString">The string you would like logged plus format specifiers.
		/// 	</param>
		/// <param name="tr">An exception to log</param>
		/// <param name="args">Variable number of Object args to be used as params to formatString.
		/// 	</param>
		public static void W(string tag, string formatString, Exception tr, params object
			[] args)
		{
			if (logger != null && IsLoggingEnabled(tag, Warn))
			{
				try
				{
					logger.W(tag, string.Format(formatString, args));
				}
				catch (Exception e)
				{
					logger.W(tag, string.Format("Unable to format log: %s", formatString), e);
				}
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
			if (logger != null)
			{
				logger.E(tag, msg);
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
			if (logger != null)
			{
				logger.E(tag, msg, tr);
			}
		}

		/// <summary>Send a ERROR message and log the exception.</summary>
		/// <remarks>Send a ERROR message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="formatString">The string you would like logged plus format specifiers.
		/// 	</param>
		/// <param name="tr">An exception to log</param>
		/// <param name="args">Variable number of Object args to be used as params to formatString.
		/// 	</param>
		public static void E(string tag, string formatString, Exception tr, params object
			[] args)
		{
			if (logger != null && IsLoggingEnabled(tag, Error))
			{
				try
				{
					logger.E(tag, string.Format(formatString, args));
				}
				catch (Exception e)
				{
					logger.E(tag, string.Format("Unable to format log: %s", formatString), e);
				}
			}
		}

		/// <summary>Send a ERROR message.</summary>
		/// <remarks>Send a ERROR message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="formatString">The string you would like logged plus format specifiers.
		/// 	</param>
		/// <param name="args">Variable number of Object args to be used as params to formatString.
		/// 	</param>
		public static void E(string tag, string formatString, params object[] args)
		{
			if (logger != null && IsLoggingEnabled(tag, Error))
			{
				try
				{
					logger.E(tag, string.Format(formatString, args));
				}
				catch (Exception e)
				{
					logger.E(tag, string.Format("Unable to format log: %s", formatString), e);
				}
			}
		}
	}
}

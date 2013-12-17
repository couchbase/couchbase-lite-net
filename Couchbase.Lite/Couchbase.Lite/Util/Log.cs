/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using System;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Util
{
	public class Log
	{
		private static readonly ILogger logger = LoggerFactory.CreateLogger();

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
	}
}

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
	public interface Logger
	{
		/// <summary>Send a VERBOSE message.</summary>
		/// <remarks>Send a VERBOSE message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		void V(string tag, string msg);

		/// <summary>Send a VERBOSE message and log the exception.</summary>
		/// <remarks>Send a VERBOSE message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		/// <param name="tr">An exception to log</param>
		void V(string tag, string msg, Exception tr);

		/// <summary>Send a DEBUG message.</summary>
		/// <remarks>Send a DEBUG message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		void D(string tag, string msg);

		/// <summary>Send a DEBUG message and log the exception.</summary>
		/// <remarks>Send a DEBUG message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		/// <param name="tr">An exception to log</param>
		void D(string tag, string msg, Exception tr);

		/// <summary>Send an INFO message.</summary>
		/// <remarks>Send an INFO message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		void I(string tag, string msg);

		/// <summary>Send a INFO message and log the exception.</summary>
		/// <remarks>Send a INFO message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		/// <param name="tr">An exception to log</param>
		void I(string tag, string msg, Exception tr);

		/// <summary>Send a WARN message.</summary>
		/// <remarks>Send a WARN message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		void W(string tag, string msg);

		void W(string tag, Exception tr);

		/// <summary>Send a WARN message and log the exception.</summary>
		/// <remarks>Send a WARN message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		/// <param name="tr">An exception to log</param>
		void W(string tag, string msg, Exception tr);

		/// <summary>Send an ERROR message.</summary>
		/// <remarks>Send an ERROR message.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		void E(string tag, string msg);

		/// <summary>Send a ERROR message and log the exception.</summary>
		/// <remarks>Send a ERROR message and log the exception.</remarks>
		/// <param name="tag">
		/// Used to identify the source of a log message.  It usually identifies
		/// the class or activity where the log call occurs.
		/// </param>
		/// <param name="msg">The message you would like logged.</param>
		/// <param name="tr">An exception to log</param>
		void E(string tag, string msg, Exception tr);
	}
}

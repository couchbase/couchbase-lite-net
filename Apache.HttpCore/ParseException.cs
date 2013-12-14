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

using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>Signals a parse error.</summary>
	/// <remarks>
	/// Signals a parse error.
	/// Parse errors when receiving a message will typically trigger
	/// <see cref="ProtocolException">ProtocolException</see>
	/// . Parse errors that do not occur during
	/// protocol execution may be handled differently.
	/// This is an unchecked exception, since there are cases where
	/// the data to be parsed has been generated and is therefore
	/// known to be parseable.
	/// </remarks>
	/// <since>4.0</since>
	[System.Serializable]
	public class ParseException : RuntimeException
	{
		private const long serialVersionUID = -7288819855864183578L;

		/// <summary>
		/// Creates a
		/// <see cref="ParseException">ParseException</see>
		/// without details.
		/// </summary>
		public ParseException() : base()
		{
		}

		/// <summary>
		/// Creates a
		/// <see cref="ParseException">ParseException</see>
		/// with a detail message.
		/// </summary>
		/// <param name="message">the exception detail message, or <code>null</code></param>
		public ParseException(string message) : base(message)
		{
		}
	}
}

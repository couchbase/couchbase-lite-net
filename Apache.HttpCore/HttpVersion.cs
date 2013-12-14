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

using Org.Apache.Http;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>Represents an HTTP version.</summary>
	/// <remarks>
	/// Represents an HTTP version. HTTP uses a "major.minor" numbering
	/// scheme to indicate versions of the protocol.
	/// <p>
	/// The version of an HTTP message is indicated by an HTTP-Version field
	/// in the first line of the message.
	/// </p>
	/// <pre>
	/// HTTP-Version   = "HTTP" "/" 1*DIGIT "." 1*DIGIT
	/// </pre>
	/// </remarks>
	/// <since>4.0</since>
	[System.Serializable]
	public sealed class HttpVersion : ProtocolVersion
	{
		private const long serialVersionUID = -5856653513894415344L;

		/// <summary>The protocol name.</summary>
		/// <remarks>The protocol name.</remarks>
		public const string Http = "HTTP";

		/// <summary>HTTP protocol version 0.9</summary>
		public static readonly Org.Apache.Http.HttpVersion Http09 = new Org.Apache.Http.HttpVersion
			(0, 9);

		/// <summary>HTTP protocol version 1.0</summary>
		public static readonly Org.Apache.Http.HttpVersion Http10 = new Org.Apache.Http.HttpVersion
			(1, 0);

		/// <summary>HTTP protocol version 1.1</summary>
		public static readonly Org.Apache.Http.HttpVersion Http11 = new Org.Apache.Http.HttpVersion
			(1, 1);

		/// <summary>Create an HTTP protocol version designator.</summary>
		/// <remarks>Create an HTTP protocol version designator.</remarks>
		/// <param name="major">the major version number of the HTTP protocol</param>
		/// <param name="minor">the minor version number of the HTTP protocol</param>
		/// <exception cref="System.ArgumentException">if either major or minor version number is negative
		/// 	</exception>
		public HttpVersion(int major, int minor) : base(Http, major, minor)
		{
		}

		/// <summary>Obtains a specific HTTP version.</summary>
		/// <remarks>Obtains a specific HTTP version.</remarks>
		/// <param name="major">the major version</param>
		/// <param name="minor">the minor version</param>
		/// <returns>
		/// an instance of
		/// <see cref="HttpVersion">HttpVersion</see>
		/// with the argument version
		/// </returns>
		public override ProtocolVersion ForVersion(int major, int minor)
		{
			if ((major == this.major) && (minor == this.minor))
			{
				return this;
			}
			if (major == 1)
			{
				if (minor == 0)
				{
					return Http10;
				}
				if (minor == 1)
				{
					return Http11;
				}
			}
			if ((major == 0) && (minor == 9))
			{
				return Http09;
			}
			// argument checking is done in the constructor
			return new Org.Apache.Http.HttpVersion(major, minor);
		}
	}
}

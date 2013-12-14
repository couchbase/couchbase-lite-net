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
using Org.Apache.Http.Entity;
using Sharpen;

namespace Org.Apache.Http.Entity
{
	/// <summary>
	/// Represents a strategy to determine length of the enclosed content entity
	/// based on properties of the HTTP message.
	/// </summary>
	/// <remarks>
	/// Represents a strategy to determine length of the enclosed content entity
	/// based on properties of the HTTP message.
	/// </remarks>
	/// <since>4.0</since>
	public abstract class ContentLengthStrategy
	{
		public const int Identity = -1;

		public const int Chunked = -2;

		/// <summary>Returns length of the given message in bytes.</summary>
		/// <remarks>
		/// Returns length of the given message in bytes. The returned value
		/// must be a non-negative number,
		/// <see cref="Identity">Identity</see>
		/// if the end of the
		/// message will be delimited by the end of connection, or
		/// <see cref="Chunked">Chunked</see>
		/// if the message is chunk coded
		/// </remarks>
		/// <param name="message">HTTP message</param>
		/// <returns>
		/// content length,
		/// <see cref="Identity">Identity</see>
		/// , or
		/// <see cref="Chunked">Chunked</see>
		/// </returns>
		/// <exception cref="Org.Apache.Http.HttpException">in case of HTTP protocol violation
		/// 	</exception>
		public abstract long DetermineLength(HttpMessage message);
	}
}

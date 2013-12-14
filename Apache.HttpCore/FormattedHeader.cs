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
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>An HTTP header which is already formatted.</summary>
	/// <remarks>
	/// An HTTP header which is already formatted.
	/// For example when headers are received, the original formatting
	/// can be preserved. This allows for the header to be sent without
	/// another formatting step.
	/// </remarks>
	/// <since>4.0</since>
	public interface FormattedHeader : Header
	{
		/// <summary>Obtains the buffer with the formatted header.</summary>
		/// <remarks>
		/// Obtains the buffer with the formatted header.
		/// The returned buffer MUST NOT be modified.
		/// </remarks>
		/// <returns>the formatted header, in a buffer that must not be modified</returns>
		CharArrayBuffer GetBuffer();

		/// <summary>
		/// Obtains the start of the header value in the
		/// <see cref="GetBuffer()">buffer</see>
		/// .
		/// By accessing the value in the buffer, creation of a temporary string
		/// can be avoided.
		/// </summary>
		/// <returns>
		/// index of the first character of the header value
		/// in the buffer returned by
		/// <see cref="GetBuffer()">getBuffer</see>
		/// .
		/// </returns>
		int GetValuePos();
	}
}

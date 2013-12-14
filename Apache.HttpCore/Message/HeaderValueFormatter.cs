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
using Org.Apache.Http.Message;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>Interface for formatting elements of a header value.</summary>
	/// <remarks>
	/// Interface for formatting elements of a header value.
	/// This is the complement to
	/// <see cref="HeaderValueParser">HeaderValueParser</see>
	/// .
	/// Instances of this interface are expected to be stateless and thread-safe.
	/// <p>
	/// All formatting methods accept an optional buffer argument.
	/// If a buffer is passed in, the formatted element will be appended
	/// and the modified buffer is returned. If no buffer is passed in,
	/// a new buffer will be created and filled with the formatted element.
	/// In both cases, the caller is allowed to modify the returned buffer.
	/// </p>
	/// </remarks>
	/// <since>4.0</since>
	public interface HeaderValueFormatter
	{
		/// <summary>Formats an array of header elements.</summary>
		/// <remarks>Formats an array of header elements.</remarks>
		/// <param name="buffer">
		/// the buffer to append to, or
		/// <code>null</code> to create a new buffer
		/// </param>
		/// <param name="elems">the header elements to format</param>
		/// <param name="quote">
		/// <code>true</code> to always format with quoted values,
		/// <code>false</code> to use quotes only when necessary
		/// </param>
		/// <returns>
		/// a buffer with the formatted header elements.
		/// If the <code>buffer</code> argument was not <code>null</code>,
		/// that buffer will be used and returned.
		/// </returns>
		CharArrayBuffer FormatElements(CharArrayBuffer buffer, HeaderElement[] elems, bool
			 quote);

		/// <summary>Formats one header element.</summary>
		/// <remarks>Formats one header element.</remarks>
		/// <param name="buffer">
		/// the buffer to append to, or
		/// <code>null</code> to create a new buffer
		/// </param>
		/// <param name="elem">the header element to format</param>
		/// <param name="quote">
		/// <code>true</code> to always format with quoted values,
		/// <code>false</code> to use quotes only when necessary
		/// </param>
		/// <returns>
		/// a buffer with the formatted header element.
		/// If the <code>buffer</code> argument was not <code>null</code>,
		/// that buffer will be used and returned.
		/// </returns>
		CharArrayBuffer FormatHeaderElement(CharArrayBuffer buffer, HeaderElement elem, bool
			 quote);

		/// <summary>Formats the parameters of a header element.</summary>
		/// <remarks>
		/// Formats the parameters of a header element.
		/// That's a list of name-value pairs, to be separated by semicolons.
		/// This method will <i>not</i> generate a leading semicolon.
		/// </remarks>
		/// <param name="buffer">
		/// the buffer to append to, or
		/// <code>null</code> to create a new buffer
		/// </param>
		/// <param name="nvps">the parameters (name-value pairs) to format</param>
		/// <param name="quote">
		/// <code>true</code> to always format with quoted values,
		/// <code>false</code> to use quotes only when necessary
		/// </param>
		/// <returns>
		/// a buffer with the formatted parameters.
		/// If the <code>buffer</code> argument was not <code>null</code>,
		/// that buffer will be used and returned.
		/// </returns>
		CharArrayBuffer FormatParameters(CharArrayBuffer buffer, NameValuePair[] nvps, bool
			 quote);

		/// <summary>Formats one name-value pair, where the value is optional.</summary>
		/// <remarks>Formats one name-value pair, where the value is optional.</remarks>
		/// <param name="buffer">
		/// the buffer to append to, or
		/// <code>null</code> to create a new buffer
		/// </param>
		/// <param name="nvp">the name-value pair to format</param>
		/// <param name="quote">
		/// <code>true</code> to always format with a quoted value,
		/// <code>false</code> to use quotes only when necessary
		/// </param>
		/// <returns>
		/// a buffer with the formatted name-value pair.
		/// If the <code>buffer</code> argument was not <code>null</code>,
		/// that buffer will be used and returned.
		/// </returns>
		CharArrayBuffer FormatNameValuePair(CharArrayBuffer buffer, NameValuePair nvp, bool
			 quote);
	}
}

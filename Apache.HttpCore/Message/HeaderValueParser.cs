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
	/// <summary>Interface for parsing header values into elements.</summary>
	/// <remarks>
	/// Interface for parsing header values into elements.
	/// Instances of this interface are expected to be stateless and thread-safe.
	/// </remarks>
	/// <since>4.0</since>
	public interface HeaderValueParser
	{
		/// <summary>Parses a header value into elements.</summary>
		/// <remarks>
		/// Parses a header value into elements.
		/// Parse errors are indicated as <code>RuntimeException</code>.
		/// <p>
		/// Some HTTP headers (such as the set-cookie header) have values that
		/// can be decomposed into multiple elements. In order to be processed
		/// by this parser, such headers must be in the following form:
		/// </p>
		/// <pre>
		/// header  = [ element ] *( "," [ element ] )
		/// element = name [ "=" [ value ] ] *( ";" [ param ] )
		/// param   = name [ "=" [ value ] ]
		/// name    = token
		/// value   = ( token | quoted-string )
		/// token         = 1*&lt;any char except "=", ",", ";", &lt;"&gt; and
		/// white space&gt;
		/// quoted-string = &lt;"&gt; *( text | quoted-char ) &lt;"&gt;
		/// text          = any char except &lt;"&gt;
		/// quoted-char   = "\" char
		/// </pre>
		/// <p>
		/// Any amount of white space is allowed between any part of the
		/// header, element or param and is ignored. A missing value in any
		/// element or param will be stored as the empty
		/// <see cref="string">string</see>
		/// ;
		/// if the "=" is also missing <var>null</var> will be stored instead.
		/// </p>
		/// </remarks>
		/// <param name="buffer">buffer holding the header value to parse</param>
		/// <param name="cursor">
		/// the parser cursor containing the current position and
		/// the bounds within the buffer for the parsing operation
		/// </param>
		/// <returns>an array holding all elements of the header value</returns>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		HeaderElement[] ParseElements(CharArrayBuffer buffer, ParserCursor cursor);

		/// <summary>Parses a single header element.</summary>
		/// <remarks>
		/// Parses a single header element.
		/// A header element consist of a semicolon-separate list
		/// of name=value definitions.
		/// </remarks>
		/// <param name="buffer">buffer holding the element to parse</param>
		/// <param name="cursor">
		/// the parser cursor containing the current position and
		/// the bounds within the buffer for the parsing operation
		/// </param>
		/// <returns>the parsed element</returns>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		HeaderElement ParseHeaderElement(CharArrayBuffer buffer, ParserCursor cursor);

		/// <summary>Parses a list of name-value pairs.</summary>
		/// <remarks>
		/// Parses a list of name-value pairs.
		/// These lists are used to specify parameters to a header element.
		/// Parse errors are indicated as <code>ParseException</code>.
		/// </remarks>
		/// <param name="buffer">buffer holding the name-value list to parse</param>
		/// <param name="cursor">
		/// the parser cursor containing the current position and
		/// the bounds within the buffer for the parsing operation
		/// </param>
		/// <returns>an array holding all items of the name-value list</returns>
		/// <exception cref="Org.Apache.Http.ParseException">in case of a parse error</exception>
		NameValuePair[] ParseParameters(CharArrayBuffer buffer, ParserCursor cursor);

		/// <summary>Parses a name=value specification, where the = and value are optional.</summary>
		/// <remarks>Parses a name=value specification, where the = and value are optional.</remarks>
		/// <param name="buffer">the buffer holding the name-value pair to parse</param>
		/// <param name="cursor">
		/// the parser cursor containing the current position and
		/// the bounds within the buffer for the parsing operation
		/// </param>
		/// <returns>
		/// the name-value pair, where the value is <code>null</code>
		/// if no value is specified
		/// </returns>
		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		NameValuePair ParseNameValuePair(CharArrayBuffer buffer, ParserCursor cursor);
	}
}

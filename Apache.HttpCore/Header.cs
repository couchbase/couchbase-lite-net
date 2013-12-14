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
	/// <summary>Represents an HTTP header field.</summary>
	/// <remarks>
	/// Represents an HTTP header field.
	/// <p>The HTTP header fields follow the same generic format as
	/// that given in Section 3.1 of RFC 822. Each header field consists
	/// of a name followed by a colon (":") and the field value. Field names
	/// are case-insensitive. The field value MAY be preceded by any amount
	/// of LWS, though a single SP is preferred.
	/// <pre>
	/// message-header = field-name ":" [ field-value ]
	/// field-name     = token
	/// field-value    = *( field-content | LWS )
	/// field-content  = &lt;the OCTETs making up the field-value
	/// and consisting of either *TEXT or combinations
	/// of token, separators, and quoted-string&gt;
	/// </pre>
	/// </remarks>
	/// <since>4.0</since>
	public interface Header
	{
		/// <summary>Get the name of the Header.</summary>
		/// <remarks>Get the name of the Header.</remarks>
		/// <returns>
		/// the name of the Header,  never
		/// <code>null</code>
		/// </returns>
		string GetName();

		/// <summary>Get the value of the Header.</summary>
		/// <remarks>Get the value of the Header.</remarks>
		/// <returns>
		/// the value of the Header,  may be
		/// <code>null</code>
		/// </returns>
		string GetValue();

		/// <summary>Parses the value.</summary>
		/// <remarks>Parses the value.</remarks>
		/// <returns>
		/// an array of
		/// <see cref="HeaderElement">HeaderElement</see>
		/// entries, may be empty, but is never
		/// <code>null</code>
		/// </returns>
		/// <exception cref="ParseException">ParseException</exception>
		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		HeaderElement[] GetElements();
	}
}

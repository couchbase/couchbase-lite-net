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
using Org.Apache.Http;
using Org.Apache.Http.Params;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>
	/// HTTP messages consist of requests from client to server and responses
	/// from server to client.
	/// </summary>
	/// <remarks>
	/// HTTP messages consist of requests from client to server and responses
	/// from server to client.
	/// <pre>
	/// HTTP-message   = Request | Response     ; HTTP/1.1 messages
	/// </pre>
	/// <p>
	/// HTTP messages use the generic message format of RFC 822 for
	/// transferring entities (the payload of the message). Both types
	/// of message consist of a start-line, zero or more header fields
	/// (also known as "headers"), an empty line (i.e., a line with nothing
	/// preceding the CRLF) indicating the end of the header fields,
	/// and possibly a message-body.
	/// </p>
	/// <pre>
	/// generic-message = start-line
	/// *(message-header CRLF)
	/// CRLF
	/// [ message-body ]
	/// start-line      = Request-Line | Status-Line
	/// </pre>
	/// </remarks>
	/// <since>4.0</since>
	public interface HttpMessage
	{
		/// <summary>Returns the protocol version this message is compatible with.</summary>
		/// <remarks>Returns the protocol version this message is compatible with.</remarks>
		ProtocolVersion GetProtocolVersion();

		/// <summary>Checks if a certain header is present in this message.</summary>
		/// <remarks>
		/// Checks if a certain header is present in this message. Header values are
		/// ignored.
		/// </remarks>
		/// <param name="name">the header name to check for.</param>
		/// <returns>true if at least one header with this name is present.</returns>
		bool ContainsHeader(string name);

		/// <summary>Returns all the headers with a specified name of this message.</summary>
		/// <remarks>
		/// Returns all the headers with a specified name of this message. Header values
		/// are ignored. Headers are orderd in the sequence they will be sent over a
		/// connection.
		/// </remarks>
		/// <param name="name">the name of the headers to return.</param>
		/// <returns>the headers whose name property equals <code>name</code>.</returns>
		Header[] GetHeaders(string name);

		/// <summary>Returns the first header with a specified name of this message.</summary>
		/// <remarks>
		/// Returns the first header with a specified name of this message. Header
		/// values are ignored. If there is more than one matching header in the
		/// message the first element of
		/// <see cref="GetHeaders(string)">GetHeaders(string)</see>
		/// is returned.
		/// If there is no matching header in the message <code>null</code> is
		/// returned.
		/// </remarks>
		/// <param name="name">the name of the header to return.</param>
		/// <returns>
		/// the first header whose name property equals <code>name</code>
		/// or <code>null</code> if no such header could be found.
		/// </returns>
		Header GetFirstHeader(string name);

		/// <summary>Returns the last header with a specified name of this message.</summary>
		/// <remarks>
		/// Returns the last header with a specified name of this message. Header values
		/// are ignored. If there is more than one matching header in the message the
		/// last element of
		/// <see cref="GetHeaders(string)">GetHeaders(string)</see>
		/// is returned. If there is no
		/// matching header in the message <code>null</code> is returned.
		/// </remarks>
		/// <param name="name">the name of the header to return.</param>
		/// <returns>
		/// the last header whose name property equals <code>name</code>.
		/// or <code>null</code> if no such header could be found.
		/// </returns>
		Header GetLastHeader(string name);

		/// <summary>Returns all the headers of this message.</summary>
		/// <remarks>
		/// Returns all the headers of this message. Headers are orderd in the sequence
		/// they will be sent over a connection.
		/// </remarks>
		/// <returns>all the headers of this message</returns>
		Header[] GetAllHeaders();

		/// <summary>Adds a header to this message.</summary>
		/// <remarks>
		/// Adds a header to this message. The header will be appended to the end of
		/// the list.
		/// </remarks>
		/// <param name="header">the header to append.</param>
		void AddHeader(Header header);

		/// <summary>Adds a header to this message.</summary>
		/// <remarks>
		/// Adds a header to this message. The header will be appended to the end of
		/// the list.
		/// </remarks>
		/// <param name="name">the name of the header.</param>
		/// <param name="value">the value of the header.</param>
		void AddHeader(string name, string value);

		/// <summary>Overwrites the first header with the same name.</summary>
		/// <remarks>
		/// Overwrites the first header with the same name. The new header will be appended to
		/// the end of the list, if no header with the given name can be found.
		/// </remarks>
		/// <param name="header">the header to set.</param>
		void SetHeader(Header header);

		/// <summary>Overwrites the first header with the same name.</summary>
		/// <remarks>
		/// Overwrites the first header with the same name. The new header will be appended to
		/// the end of the list, if no header with the given name can be found.
		/// </remarks>
		/// <param name="name">the name of the header.</param>
		/// <param name="value">the value of the header.</param>
		void SetHeader(string name, string value);

		/// <summary>Overwrites all the headers in the message.</summary>
		/// <remarks>Overwrites all the headers in the message.</remarks>
		/// <param name="headers">the array of headers to set.</param>
		void SetHeaders(Header[] headers);

		/// <summary>Removes a header from this message.</summary>
		/// <remarks>Removes a header from this message.</remarks>
		/// <param name="header">the header to remove.</param>
		void RemoveHeader(Header header);

		/// <summary>Removes all headers with a certain name from this message.</summary>
		/// <remarks>Removes all headers with a certain name from this message.</remarks>
		/// <param name="name">The name of the headers to remove.</param>
		void RemoveHeaders(string name);

		/// <summary>Returns an iterator of all the headers.</summary>
		/// <remarks>Returns an iterator of all the headers.</remarks>
		/// <returns>
		/// Iterator that returns Header objects in the sequence they are
		/// sent over a connection.
		/// </returns>
		Org.Apache.Http.HeaderIterator HeaderIterator();

		/// <summary>Returns an iterator of the headers with a given name.</summary>
		/// <remarks>Returns an iterator of the headers with a given name.</remarks>
		/// <param name="name">
		/// the name of the headers over which to iterate, or
		/// <code>null</code> for all headers
		/// </param>
		/// <returns>
		/// Iterator that returns Header objects with the argument name
		/// in the sequence they are sent over a connection.
		/// </returns>
		Org.Apache.Http.HeaderIterator HeaderIterator(string name);

		/// <summary>
		/// Returns the parameters effective for this message as set by
		/// <see cref="SetParams(Org.Apache.Http.Params.HttpParams)">SetParams(Org.Apache.Http.Params.HttpParams)
		/// 	</see>
		/// .
		/// </summary>
		[System.ObsoleteAttribute(@"(4.3) use configuration classes provided 'org.apache.http.config' and 'org.apache.http.client.config'"
			)]
		HttpParams GetParams();

		/// <summary>Provides parameters to be used for the processing of this message.</summary>
		/// <remarks>Provides parameters to be used for the processing of this message.</remarks>
		/// <param name="params">the parameters</param>
		[System.ObsoleteAttribute(@"(4.3) use configuration classes provided 'org.apache.http.config' and 'org.apache.http.client.config'"
			)]
		void SetParams(HttpParams @params);
	}
}

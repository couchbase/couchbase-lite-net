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
using System.IO;
using Org.Apache.Http;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>An entity that can be sent or received with an HTTP message.</summary>
	/// <remarks>
	/// An entity that can be sent or received with an HTTP message.
	/// Entities can be found in some
	/// <see cref="HttpEntityEnclosingRequest">requests</see>
	/// and in
	/// <see cref="HttpResponse">responses</see>
	/// , where they are optional.
	/// <p>
	/// There are three distinct types of entities in HttpCore,
	/// depending on where their
	/// <see cref="GetContent()">content</see>
	/// originates:
	/// <ul>
	/// <li><b>streamed</b>: The content is received from a stream, or
	/// generated on the fly. In particular, this category includes
	/// entities being received from a
	/// <see cref="HttpConnection">connection</see>
	/// .
	/// <see cref="IsStreaming()">Streamed</see>
	/// entities are generally not
	/// <see cref="IsRepeatable()">repeatable</see>
	/// .
	/// </li>
	/// <li><b>self-contained</b>: The content is in memory or obtained by
	/// means that are independent from a connection or other entity.
	/// Self-contained entities are generally
	/// <see cref="IsRepeatable()">repeatable</see>
	/// .
	/// </li>
	/// <li><b>wrapping</b>: The content is obtained from another entity.
	/// </li>
	/// </ul>
	/// This distinction is important for connection management with incoming
	/// entities. For entities that are created by an application and only sent
	/// using the HTTP components framework, the difference between streamed
	/// and self-contained is of little importance. In that case, it is suggested
	/// to consider non-repeatable entities as streamed, and those that are
	/// repeatable (without a huge effort) as self-contained.
	/// </remarks>
	/// <since>4.0</since>
	public interface HttpEntity
	{
		/// <summary>Tells if the entity is capable of producing its data more than once.</summary>
		/// <remarks>
		/// Tells if the entity is capable of producing its data more than once.
		/// A repeatable entity's getContent() and writeTo(OutputStream) methods
		/// can be called more than once whereas a non-repeatable entity's can not.
		/// </remarks>
		/// <returns>true if the entity is repeatable, false otherwise.</returns>
		bool IsRepeatable();

		/// <summary>Tells about chunked encoding for this entity.</summary>
		/// <remarks>
		/// Tells about chunked encoding for this entity.
		/// The primary purpose of this method is to indicate whether
		/// chunked encoding should be used when the entity is sent.
		/// For entities that are received, it can also indicate whether
		/// the entity was received with chunked encoding.
		/// <br/>
		/// The behavior of wrapping entities is implementation dependent,
		/// but should respect the primary purpose.
		/// </remarks>
		/// <returns>
		/// <code>true</code> if chunked encoding is preferred for this
		/// entity, or <code>false</code> if it is not
		/// </returns>
		bool IsChunked();

		/// <summary>Tells the length of the content, if known.</summary>
		/// <remarks>Tells the length of the content, if known.</remarks>
		/// <returns>
		/// the number of bytes of the content, or
		/// a negative number if unknown. If the content length is known
		/// but exceeds
		/// <see cref="long.MaxValue">Long.MAX_VALUE</see>
		/// ,
		/// a negative number is returned.
		/// </returns>
		long GetContentLength();

		/// <summary>Obtains the Content-Type header, if known.</summary>
		/// <remarks>
		/// Obtains the Content-Type header, if known.
		/// This is the header that should be used when sending the entity,
		/// or the one that was received with the entity. It can include a
		/// charset attribute.
		/// </remarks>
		/// <returns>
		/// the Content-Type header for this entity, or
		/// <code>null</code> if the content type is unknown
		/// </returns>
		Header GetContentType();

		/// <summary>Obtains the Content-Encoding header, if known.</summary>
		/// <remarks>
		/// Obtains the Content-Encoding header, if known.
		/// This is the header that should be used when sending the entity,
		/// or the one that was received with the entity.
		/// Wrapping entities that modify the content encoding should
		/// adjust this header accordingly.
		/// </remarks>
		/// <returns>
		/// the Content-Encoding header for this entity, or
		/// <code>null</code> if the content encoding is unknown
		/// </returns>
		Header GetContentEncoding();

		/// <summary>Returns a content stream of the entity.</summary>
		/// <remarks>
		/// Returns a content stream of the entity.
		/// <see cref="IsRepeatable()">Repeatable</see>
		/// entities are expected
		/// to create a new instance of
		/// <see cref="System.IO.InputStream">System.IO.InputStream</see>
		/// for each invocation
		/// of this method and therefore can be consumed multiple times.
		/// Entities that are not
		/// <see cref="IsRepeatable()">repeatable</see>
		/// are expected
		/// to return the same
		/// <see cref="System.IO.InputStream">System.IO.InputStream</see>
		/// instance and therefore
		/// may not be consumed more than once.
		/// <p>
		/// IMPORTANT: Please note all entity implementations must ensure that
		/// all allocated resources are properly deallocated after
		/// the
		/// <see cref="System.IO.InputStream.Close()">System.IO.InputStream.Close()</see>
		/// method is invoked.
		/// </remarks>
		/// <returns>content stream of the entity.</returns>
		/// <exception cref="System.IO.IOException">if the stream could not be created</exception>
		/// <exception cref="System.InvalidOperationException">if content stream cannot be created.
		/// 	</exception>
		/// <seealso cref="IsRepeatable()">IsRepeatable()</seealso>
		InputStream GetContent();

		/// <summary>Writes the entity content out to the output stream.</summary>
		/// <remarks>
		/// Writes the entity content out to the output stream.
		/// <p>
		/// <p>
		/// IMPORTANT: Please note all entity implementations must ensure that
		/// all allocated resources are properly deallocated when this method
		/// returns.
		/// </remarks>
		/// <param name="outstream">the output stream to write entity content to</param>
		/// <exception cref="System.IO.IOException">if an I/O error occurs</exception>
		void WriteTo(OutputStream outstream);

		/// <summary>Tells whether this entity depends on an underlying stream.</summary>
		/// <remarks>
		/// Tells whether this entity depends on an underlying stream.
		/// Streamed entities that read data directly from the socket should
		/// return <code>true</code>. Self-contained entities should return
		/// <code>false</code>. Wrapping entities should delegate this call
		/// to the wrapped entity.
		/// </remarks>
		/// <returns>
		/// <code>true</code> if the entity content is streamed,
		/// <code>false</code> otherwise
		/// </returns>
		bool IsStreaming();

		// don't expect an exception here
		/// <summary>This method is deprecated since version 4.1.</summary>
		/// <remarks>
		/// This method is deprecated since version 4.1. Please use standard
		/// java convention to ensure resource deallocation by calling
		/// <see cref="System.IO.InputStream.Close()">System.IO.InputStream.Close()</see>
		/// on the input stream returned by
		/// <see cref="GetContent()">GetContent()</see>
		/// <p>
		/// This method is called to indicate that the content of this entity
		/// is no longer required. All entity implementations are expected to
		/// release all allocated resources as a result of this method
		/// invocation. Content streaming entities are also expected to
		/// dispose of the remaining content, if any. Wrapping entities should
		/// delegate this call to the wrapped entity.
		/// <p>
		/// This method is of particular importance for entities being
		/// received from a
		/// <see cref="HttpConnection">connection</see>
		/// . The entity
		/// needs to be consumed completely in order to re-use the connection
		/// with keep-alive.
		/// </remarks>
		/// <exception cref="System.IO.IOException">if an I/O error occurs.</exception>
		/// <seealso cref="GetContent()">and #writeTo(OutputStream)</seealso>
		[System.ObsoleteAttribute(@"(4.1) Use Org.Apache.Http.Util.EntityUtils.Consume(HttpEntity)"
			)]
		void ConsumeContent();
	}
}

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

using Org.Apache.Http.Entity.Mime.Content;
using Sharpen;

namespace Org.Apache.Http.Entity.Mime.Content
{
	/// <summary>Represents common content properties.</summary>
	/// <remarks>Represents common content properties.</remarks>
	public interface ContentDescriptor
	{
		/// <summary>Returns the body descriptors MIME type.</summary>
		/// <remarks>Returns the body descriptors MIME type.</remarks>
		/// <seealso cref="GetMediaType()">GetMediaType()</seealso>
		/// <seealso cref="GetSubType()">GetSubType()</seealso>
		/// <returns>
		/// The MIME type, which has been parsed from the
		/// content-type definition. Must not be null, but
		/// "text/plain", if no content-type was specified.
		/// </returns>
		string GetMimeType();

		/// <summary>Gets the defaulted MIME media type for this content.</summary>
		/// <remarks>
		/// Gets the defaulted MIME media type for this content.
		/// For example <code>TEXT</code>, <code>IMAGE</code>, <code>MULTIPART</code>
		/// </remarks>
		/// <seealso cref="GetMimeType()">GetMimeType()</seealso>
		/// <returns>
		/// the MIME media type when content-type specified,
		/// otherwise the correct default (<code>TEXT</code>)
		/// </returns>
		string GetMediaType();

		/// <summary>Gets the defaulted MIME sub type for this content.</summary>
		/// <remarks>Gets the defaulted MIME sub type for this content.</remarks>
		/// <seealso cref="GetMimeType()">GetMimeType()</seealso>
		/// <returns>
		/// the MIME media type when content-type is specified,
		/// otherwise the correct default (<code>PLAIN</code>)
		/// </returns>
		string GetSubType();

		/// <summary>
		/// <p>The body descriptors character set, defaulted appropriately for the MIME type.</p>
		/// <p>
		/// For <code>TEXT</code> types, this will be defaulted to <code>us-ascii</code>.
		/// </summary>
		/// <remarks>
		/// <p>The body descriptors character set, defaulted appropriately for the MIME type.</p>
		/// <p>
		/// For <code>TEXT</code> types, this will be defaulted to <code>us-ascii</code>.
		/// For other types, when the charset parameter is missing this property will be null.
		/// </p>
		/// </remarks>
		/// <returns>
		/// Character set, which has been parsed from the
		/// content-type definition. Not null for <code>TEXT</code> types, when unset will
		/// be set to default <code>us-ascii</code>. For other types, when unset,
		/// null will be returned.
		/// </returns>
		string GetCharset();

		/// <summary>Returns the body descriptors transfer encoding.</summary>
		/// <remarks>Returns the body descriptors transfer encoding.</remarks>
		/// <returns>
		/// The transfer encoding. Must not be null, but "7bit",
		/// if no transfer-encoding was specified.
		/// </returns>
		string GetTransferEncoding();

		/// <summary>Returns the body descriptors content-length.</summary>
		/// <remarks>Returns the body descriptors content-length.</remarks>
		/// <returns>
		/// Content length, if known, or -1, to indicate the absence of a
		/// content-length header.
		/// </returns>
		long GetContentLength();
	}
}

//
// ContentDescriptor.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using Apache.Http.Entity.Mime.Content;
using Sharpen;

namespace Apache.Http.Entity.Mime.Content
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

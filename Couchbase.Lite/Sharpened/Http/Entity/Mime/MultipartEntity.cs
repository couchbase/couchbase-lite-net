//
// MultipartEntity.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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
using System.Text;
using Org.Apache.Http;
using Org.Apache.Http.Entity.Mime;
using Org.Apache.Http.Entity.Mime.Content;
using Org.Apache.Http.Message;
using Org.Apache.Http.Protocol;
using Sharpen;

namespace Org.Apache.Http.Entity.Mime
{
	/// <summary>Multipart/form coded HTTP entity consisting of multiple body parts.</summary>
	/// <remarks>Multipart/form coded HTTP entity consisting of multiple body parts.</remarks>
	/// <since>4.0</since>
	public class MultipartEntity : HttpEntity
	{
		/// <summary>The pool of ASCII chars to be used for generating a multipart boundary.</summary>
		/// <remarks>The pool of ASCII chars to be used for generating a multipart boundary.</remarks>
		private static readonly char[] MultipartChars = "-_1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
			.ToCharArray();

		private readonly HttpMultipart multipart;

		private readonly Header contentType;

		private long length;

		private volatile bool dirty;

		/// <summary>Creates an instance using the specified parameters</summary>
		/// <param name="mode">
		/// the mode to use, may be
		/// <code>null</code>
		/// , in which case
		/// <see cref="HttpMultipartMode.Strict">HttpMultipartMode.Strict</see>
		/// is used
		/// </param>
		/// <param name="boundary">
		/// the boundary string, may be
		/// <code>null</code>
		/// , in which case
		/// <see cref="GenerateBoundary()">GenerateBoundary()</see>
		/// is invoked to create the string
		/// </param>
		/// <param name="charset">
		/// the character set to use, may be
		/// <code>null</code>
		/// , in which case
		/// <see cref="MIME.DefaultCharset">MIME.DefaultCharset</see>
		/// - i.e. US-ASCII - is used.
		/// </param>
		public MultipartEntity(HttpMultipartMode mode, string boundary, Encoding charset)
			 : base()
		{
			// @GuardedBy("dirty") // we always read dirty before accessing length
			// used to decide whether to recalculate length
			if (boundary == null)
			{
				boundary = GenerateBoundary();
			}
			if (mode == null)
			{
				mode = HttpMultipartMode.Strict;
			}
			this.multipart = new HttpMultipart("related", charset, boundary, mode);
			this.contentType = new BasicHeader(HTTP.ContentType, GenerateContentType(boundary
				, charset));
			this.dirty = true;
		}

		/// <summary>
		/// Creates an instance using the specified
		/// <see cref="HttpMultipartMode">HttpMultipartMode</see>
		/// mode.
		/// Boundary and charset are set to
		/// <code>null</code>
		/// .
		/// </summary>
		/// <param name="mode">the desired mode</param>
		public MultipartEntity(HttpMultipartMode mode) : this(mode, null, null)
		{
		}

		/// <summary>
		/// Creates an instance using mode
		/// <see cref="HttpMultipartMode.Strict">HttpMultipartMode.Strict</see>
		/// </summary>
		public MultipartEntity() : this(HttpMultipartMode.Strict, null, null)
		{
		}

		protected internal virtual string GenerateContentType(string boundary, Encoding charset
			)
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append("multipart/related; boundary=");
			buffer.Append(boundary);
			if (charset != null)
			{
				buffer.Append("; charset=");
				buffer.Append(charset.Name());
			}
			return buffer.ToString();
		}

		protected internal virtual string GenerateBoundary()
		{
			StringBuilder buffer = new StringBuilder();
			Random rand = new Random();
			int count = rand.Next(11) + 30;
			// a random size from 30 to 40
			for (int i = 0; i < count; i++)
			{
				buffer.Append(MultipartChars[rand.Next(MultipartChars.Length)]);
			}
			return buffer.ToString();
		}

		public virtual void AddPart(FormBodyPart bodyPart)
		{
			this.multipart.AddBodyPart(bodyPart);
			this.dirty = true;
		}

		public virtual void AddPart(string name, ContentBody contentBody)
		{
			AddPart(new FormBodyPart(name, contentBody));
		}

		public virtual bool IsRepeatable()
		{
			foreach (FormBodyPart part in this.multipart.GetBodyParts())
			{
				ContentBody body = part.GetBody();
				if (body.GetContentLength() < 0)
				{
					return false;
				}
			}
			return true;
		}

		public virtual bool IsChunked()
		{
			return !IsRepeatable();
		}

		public virtual bool IsStreaming()
		{
			return !IsRepeatable();
		}

		public virtual long GetContentLength()
		{
			if (this.dirty)
			{
				this.length = this.multipart.GetTotalLength();
				this.dirty = false;
			}
			return this.length;
		}

		public virtual Header GetContentType()
		{
			return this.contentType;
		}

		public virtual Header GetContentEncoding()
		{
			return null;
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="System.NotSupportedException"></exception>
		public virtual void ConsumeContent()
		{
			if (IsStreaming())
			{
				throw new NotSupportedException("Streaming entity does not implement #consumeContent()"
					);
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="System.NotSupportedException"></exception>
		public virtual InputStream GetContent()
		{
			throw new NotSupportedException("Multipart form entity does not implement #getContent()"
				);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void WriteTo(OutputStream outstream)
		{
			this.multipart.WriteTo(outstream);
		}
	}
}

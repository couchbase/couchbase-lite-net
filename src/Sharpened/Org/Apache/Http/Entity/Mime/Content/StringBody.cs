//
// StringBody.cs
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

using System;
using System.IO;
using System.Text;
using Org.Apache.Http.Entity.Mime;
using Org.Apache.Http.Entity.Mime.Content;
using Sharpen;

namespace Org.Apache.Http.Entity.Mime.Content
{
	/// <since>4.0</since>
	public class StringBody : AbstractContentBody
	{
		private readonly byte[] content;

		private readonly Encoding charset;

		/// <since>4.1</since>
		/// <exception cref="System.ArgumentException"></exception>
		public static Org.Apache.Http.Entity.Mime.Content.StringBody Create(string text, 
			string mimeType, Encoding charset)
		{
			try
			{
				return new Org.Apache.Http.Entity.Mime.Content.StringBody(text, mimeType, charset
					);
			}
			catch (UnsupportedEncodingException ex)
			{
				throw new ArgumentException("Charset " + charset + " is not supported", ex);
			}
		}

		/// <since>4.1</since>
		/// <exception cref="System.ArgumentException"></exception>
		public static Org.Apache.Http.Entity.Mime.Content.StringBody Create(string text, 
			Encoding charset)
		{
			return Create(text, null, charset);
		}

		/// <since>4.1</since>
		/// <exception cref="System.ArgumentException"></exception>
		public static Org.Apache.Http.Entity.Mime.Content.StringBody Create(string text)
		{
			return Create(text, null, null);
		}

		/// <summary>Create a StringBody from the specified text, mime type and character set.
		/// 	</summary>
		/// <remarks>Create a StringBody from the specified text, mime type and character set.
		/// 	</remarks>
		/// <param name="text">
		/// to be used for the body, not
		/// <code>null</code>
		/// </param>
		/// <param name="mimeType">
		/// the mime type, not
		/// <code>null</code>
		/// </param>
		/// <param name="charset">
		/// the character set, may be
		/// <code>null</code>
		/// , in which case the US-ASCII charset is used
		/// </param>
		/// <exception cref="System.IO.UnsupportedEncodingException">System.IO.UnsupportedEncodingException
		/// 	</exception>
		/// <exception cref="System.ArgumentException">
		/// if the
		/// <code>text</code>
		/// parameter is null
		/// </exception>
		public StringBody(string text, string mimeType, Encoding charset) : base(mimeType
			)
		{
			if (text == null)
			{
				throw new ArgumentException("Text may not be null");
			}
			if (charset == null)
			{
				charset = Sharpen.Extensions.GetEncoding("US-ASCII");
			}
			this.content = Sharpen.Runtime.GetBytesForString(text, charset.Name());
			this.charset = charset;
		}

		/// <summary>Create a StringBody from the specified text and character set.</summary>
		/// <remarks>
		/// Create a StringBody from the specified text and character set.
		/// The mime type is set to "text/plain".
		/// </remarks>
		/// <param name="text">
		/// to be used for the body, not
		/// <code>null</code>
		/// </param>
		/// <param name="charset">
		/// the character set, may be
		/// <code>null</code>
		/// , in which case the US-ASCII charset is used
		/// </param>
		/// <exception cref="System.IO.UnsupportedEncodingException">System.IO.UnsupportedEncodingException
		/// 	</exception>
		/// <exception cref="System.ArgumentException">
		/// if the
		/// <code>text</code>
		/// parameter is null
		/// </exception>
		public StringBody(string text, Encoding charset) : this(text, "text/plain", charset
			)
		{
		}

		/// <summary>Create a StringBody from the specified text.</summary>
		/// <remarks>
		/// Create a StringBody from the specified text.
		/// The mime type is set to "text/plain".
		/// The hosts default charset is used.
		/// </remarks>
		/// <param name="text">
		/// to be used for the body, not
		/// <code>null</code>
		/// </param>
		/// <exception cref="System.IO.UnsupportedEncodingException">System.IO.UnsupportedEncodingException
		/// 	</exception>
		/// <exception cref="System.ArgumentException">
		/// if the
		/// <code>text</code>
		/// parameter is null
		/// </exception>
		public StringBody(string text) : this(text, "text/plain", null)
		{
		}

		public virtual StreamReader GetReader()
		{
			return new InputStreamReader(new ByteArrayInputStream(this.content), this.charset
				);
		}

		/// <exception cref="System.IO.IOException"></exception>
		[Obsolete]
		[System.ObsoleteAttribute(@"use WriteTo(System.IO.OutputStream)")]
		public virtual void WriteTo(OutputStream @out, int mode)
		{
			WriteTo(@out);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void WriteTo(OutputStream @out)
		{
			if (@out == null)
			{
				throw new ArgumentException("Output stream may not be null");
			}
			InputStream @in = new ByteArrayInputStream(this.content);
			byte[] tmp = new byte[4096];
			int l;
			while ((l = @in.Read(tmp)) != -1)
			{
				@out.Write(tmp, 0, l);
			}
			@out.Flush();
		}

		public override string GetTransferEncoding()
		{
			return MIME.Enc8bit;
		}

		public override string GetCharset()
		{
			return this.charset.Name();
		}

		public override long GetContentLength()
		{
			return this.content.Length;
		}

		public override string GetFilename()
		{
			return null;
		}
	}
}

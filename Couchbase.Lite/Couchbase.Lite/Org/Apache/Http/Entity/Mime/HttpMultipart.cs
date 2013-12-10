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
using System.Collections.Generic;
using System.IO;
using System.Text;
using Org.Apache.Http.Entity.Mime;
using Org.Apache.Http.Entity.Mime.Content;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Entity.Mime
{
	/// <summary>HttpMultipart represents a collection of MIME multipart encoded content bodies.
	/// 	</summary>
	/// <remarks>
	/// HttpMultipart represents a collection of MIME multipart encoded content bodies. This class is
	/// capable of operating either in the strict (RFC 822, RFC 2045, RFC 2046 compliant) or
	/// the browser compatible modes.
	/// </remarks>
	/// <since>4.0</since>
	public class HttpMultipart
	{
		private static ByteArrayBuffer Encode(Encoding charset, string @string)
		{
			ByteBuffer encoded = charset.Encode(CharBuffer.Wrap(@string));
			ByteArrayBuffer bab = new ByteArrayBuffer(encoded.Remaining());
			bab.Append(((byte[])encoded.Array()), encoded.Position(), encoded.Remaining());
			return bab;
		}

		/// <exception cref="System.IO.IOException"></exception>
		private static void WriteBytes(ByteArrayBuffer b, OutputStream @out)
		{
			@out.Write(b.Buffer(), 0, b.Length());
		}

		/// <exception cref="System.IO.IOException"></exception>
		private static void WriteBytes(string s, Encoding charset, OutputStream @out)
		{
			ByteArrayBuffer b = Encode(charset, s);
			WriteBytes(b, @out);
		}

		/// <exception cref="System.IO.IOException"></exception>
		private static void WriteBytes(string s, OutputStream @out)
		{
			ByteArrayBuffer b = Encode(MIME.DefaultCharset, s);
			WriteBytes(b, @out);
		}

		/// <exception cref="System.IO.IOException"></exception>
		private static void WriteField(MinimalField field, OutputStream @out)
		{
			WriteBytes(field.GetName(), @out);
			WriteBytes(FieldSep, @out);
			WriteBytes(field.GetBody(), @out);
			WriteBytes(CrLf, @out);
		}

		/// <exception cref="System.IO.IOException"></exception>
		private static void WriteField(MinimalField field, Encoding charset, OutputStream
			 @out)
		{
			WriteBytes(field.GetName(), charset, @out);
			WriteBytes(FieldSep, @out);
			WriteBytes(field.GetBody(), charset, @out);
			WriteBytes(CrLf, @out);
		}

		private static readonly ByteArrayBuffer FieldSep = Encode(MIME.DefaultCharset, ": "
			);

		private static readonly ByteArrayBuffer CrLf = Encode(MIME.DefaultCharset, "\r\n"
			);

		private static readonly ByteArrayBuffer TwoDashes = Encode(MIME.DefaultCharset, "--"
			);

		private readonly string subType;

		private readonly Encoding charset;

		private readonly string boundary;

		private readonly IList<FormBodyPart> parts;

		private readonly HttpMultipartMode mode;

		/// <summary>Creates an instance with the specified settings.</summary>
		/// <remarks>Creates an instance with the specified settings.</remarks>
		/// <param name="subType">
		/// mime subtype - must not be
		/// <code>null</code>
		/// </param>
		/// <param name="charset">
		/// the character set to use. May be
		/// <code>null</code>
		/// , in which case
		/// <see cref="MIME.DefaultCharset">MIME.DefaultCharset</see>
		/// - i.e. US-ASCII - is used.
		/// </param>
		/// <param name="boundary">
		/// to use  - must not be
		/// <code>null</code>
		/// </param>
		/// <param name="mode">the mode to use</param>
		/// <exception cref="System.ArgumentException">if charset is null or boundary is null
		/// 	</exception>
		public HttpMultipart(string subType, Encoding charset, string boundary, HttpMultipartMode
			 mode) : base()
		{
			if (subType == null)
			{
				throw new ArgumentException("Multipart subtype may not be null");
			}
			if (boundary == null)
			{
				throw new ArgumentException("Multipart boundary may not be null");
			}
			this.subType = subType;
			this.charset = charset != null ? charset : MIME.DefaultCharset;
			this.boundary = boundary;
			this.parts = new AList<FormBodyPart>();
			this.mode = mode;
		}

		/// <summary>Creates an instance with the specified settings.</summary>
		/// <remarks>
		/// Creates an instance with the specified settings.
		/// Mode is set to
		/// <see cref="HttpMultipartMode.Strict">HttpMultipartMode.Strict</see>
		/// </remarks>
		/// <param name="subType">
		/// mime subtype - must not be
		/// <code>null</code>
		/// </param>
		/// <param name="charset">
		/// the character set to use. May be
		/// <code>null</code>
		/// , in which case
		/// <see cref="MIME.DefaultCharset">MIME.DefaultCharset</see>
		/// - i.e. US-ASCII - is used.
		/// </param>
		/// <param name="boundary">
		/// to use  - must not be
		/// <code>null</code>
		/// </param>
		/// <exception cref="System.ArgumentException">if charset is null or boundary is null
		/// 	</exception>
		public HttpMultipart(string subType, Encoding charset, string boundary) : this(subType
			, charset, boundary, HttpMultipartMode.Strict)
		{
		}

		public HttpMultipart(string subType, string boundary) : this(subType, null, boundary
			)
		{
		}

		public virtual string GetSubType()
		{
			return this.subType;
		}

		public virtual Encoding GetCharset()
		{
			return this.charset;
		}

		public virtual HttpMultipartMode GetMode()
		{
			return this.mode;
		}

		public virtual IList<FormBodyPart> GetBodyParts()
		{
			return this.parts;
		}

		public virtual void AddBodyPart(FormBodyPart part)
		{
			if (part == null)
			{
				return;
			}
			this.parts.AddItem(part);
		}

		public virtual string GetBoundary()
		{
			return this.boundary;
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void DoWriteTo(HttpMultipartMode mode, OutputStream @out, bool writeContent
			)
		{
			ByteArrayBuffer boundary = Encode(this.charset, GetBoundary());
			foreach (FormBodyPart part in this.parts)
			{
				WriteBytes(TwoDashes, @out);
				WriteBytes(boundary, @out);
				WriteBytes(CrLf, @out);
				Header header = part.GetHeader();
				switch (mode)
				{
					case HttpMultipartMode.Strict:
					{
						foreach (MinimalField field in header)
						{
							WriteField(field, @out);
						}
						break;
					}

					case HttpMultipartMode.BrowserCompatible:
					{
						// Only write Content-Disposition
						// Use content charset
						MinimalField cd = part.GetHeader().GetField(MIME.ContentDisposition);
						WriteField(cd, this.charset, @out);
						string filename = part.GetBody().GetFilename();
						if (filename != null)
						{
							MinimalField ct = part.GetHeader().GetField(MIME.ContentType);
							WriteField(ct, this.charset, @out);
						}
						break;
					}
				}
				WriteBytes(CrLf, @out);
				if (writeContent)
				{
					part.GetBody().WriteTo(@out);
				}
				WriteBytes(CrLf, @out);
			}
			WriteBytes(TwoDashes, @out);
			WriteBytes(boundary, @out);
			WriteBytes(TwoDashes, @out);
			WriteBytes(CrLf, @out);
		}

		/// <summary>Writes out the content in the multipart/form encoding.</summary>
		/// <remarks>
		/// Writes out the content in the multipart/form encoding. This method
		/// produces slightly different formatting depending on its compatibility
		/// mode.
		/// </remarks>
		/// <seealso cref="GetMode()">GetMode()</seealso>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void WriteTo(OutputStream @out)
		{
			DoWriteTo(this.mode, @out, true);
		}

		/// <summary>
		/// Determines the total length of the multipart content (content length of
		/// individual parts plus that of extra elements required to delimit the parts
		/// from one another).
		/// </summary>
		/// <remarks>
		/// Determines the total length of the multipart content (content length of
		/// individual parts plus that of extra elements required to delimit the parts
		/// from one another). If any of the @{link BodyPart}s contained in this object
		/// is of a streaming entity of unknown length the total length is also unknown.
		/// <p/>
		/// This method buffers only a small amount of data in order to determine the
		/// total length of the entire entity. The content of individual parts is not
		/// buffered.
		/// </remarks>
		/// <returns>
		/// total length of the multipart entity if known, <code>-1</code>
		/// otherwise.
		/// </returns>
		public virtual long GetTotalLength()
		{
			long contentLen = 0;
			foreach (FormBodyPart part in this.parts)
			{
				ContentBody body = part.GetBody();
				long len = body.GetContentLength();
				if (len >= 0)
				{
					contentLen += len;
				}
				else
				{
					return -1;
				}
			}
			ByteArrayOutputStream @out = new ByteArrayOutputStream();
			try
			{
				DoWriteTo(this.mode, @out, false);
				byte[] extra = @out.ToByteArray();
				return contentLen + extra.Length;
			}
			catch (IOException)
			{
				// Should never happen
				return -1;
			}
		}
	}
}

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
using System.Text;
using Org.Apache.Http.Entity;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Entity
{
	/// <summary>
	/// A self contained, repeatable entity that obtains its content from
	/// a
	/// <see cref="string">string</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	public class StringEntity : AbstractHttpEntity, ICloneable
	{
		protected internal readonly byte[] content;

		/// <summary>Creates a StringEntity with the specified content and content type.</summary>
		/// <remarks>Creates a StringEntity with the specified content and content type.</remarks>
		/// <param name="string">
		/// content to be used. Not
		/// <code>null</code>
		/// .
		/// </param>
		/// <param name="contentType">
		/// content type to be used. May be
		/// <code>null</code>
		/// , in which case the default
		/// MIME type
		/// <see cref="ContentType.TextPlain">ContentType.TextPlain</see>
		/// is assumed.
		/// </param>
		/// <exception cref="System.ArgumentException">if the string parameter is null</exception>
		/// <exception cref="Sharpen.UnsupportedCharsetException">
		/// Thrown when the named charset is not available in
		/// this instance of the Java virtual machine
		/// </exception>
		/// <since>4.2</since>
		public StringEntity(string @string, ContentType contentType) : base()
		{
			Args.NotNull(@string, "Source string");
			Encoding charset = contentType != null ? contentType.GetCharset() : null;
			if (charset == null)
			{
				charset = HTTP.DefContentCharset;
			}
			try
			{
				this.content = Sharpen.Runtime.GetBytesForString(@string, charset.Name());
			}
			catch (UnsupportedEncodingException)
			{
				// should never happen
				throw new UnsupportedCharsetException(charset.Name());
			}
			if (contentType != null)
			{
				SetContentType(contentType.ToString());
			}
		}

		/// <summary>Creates a StringEntity with the specified content, MIME type and charset
		/// 	</summary>
		/// <param name="string">
		/// content to be used. Not
		/// <code>null</code>
		/// .
		/// </param>
		/// <param name="mimeType">
		/// MIME type to be used. May be
		/// <code>null</code>
		/// , in which case the default
		/// is
		/// <see cref="Org.Apache.Http.Protocol.HTTP.PlainTextType">Org.Apache.Http.Protocol.HTTP.PlainTextType
		/// 	</see>
		/// i.e. "text/plain"
		/// </param>
		/// <param name="charset">
		/// character set to be used. May be
		/// <code>null</code>
		/// , in which case the default
		/// is
		/// <see cref="Org.Apache.Http.Protocol.HTTP.DefContentCharset">Org.Apache.Http.Protocol.HTTP.DefContentCharset
		/// 	</see>
		/// i.e. "ISO-8859-1"
		/// </param>
		/// <exception cref="System.IO.UnsupportedEncodingException">If the named charset is not supported.
		/// 	</exception>
		/// <since>4.1</since>
		/// <exception cref="System.ArgumentException">if the string parameter is null</exception>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1.3) use StringEntity(string, ContentType)")]
		public StringEntity(string @string, string mimeType, string charset) : base()
		{
			Args.NotNull(@string, "Source string");
			string mt = mimeType != null ? mimeType : HTTP.PlainTextType;
			string cs = charset != null ? charset : HTTP.DefaultContentCharset;
			this.content = Sharpen.Runtime.GetBytesForString(@string, cs);
			SetContentType(mt + HTTP.CharsetParam + cs);
		}

		/// <summary>Creates a StringEntity with the specified content and charset.</summary>
		/// <remarks>
		/// Creates a StringEntity with the specified content and charset. The MIME type defaults
		/// to "text/plain".
		/// </remarks>
		/// <param name="string">
		/// content to be used. Not
		/// <code>null</code>
		/// .
		/// </param>
		/// <param name="charset">
		/// character set to be used. May be
		/// <code>null</code>
		/// , in which case the default
		/// is
		/// <see cref="Org.Apache.Http.Protocol.HTTP.DefContentCharset">Org.Apache.Http.Protocol.HTTP.DefContentCharset
		/// 	</see>
		/// is assumed
		/// </param>
		/// <exception cref="System.ArgumentException">if the string parameter is null</exception>
		/// <exception cref="Sharpen.UnsupportedCharsetException">
		/// Thrown when the named charset is not available in
		/// this instance of the Java virtual machine
		/// </exception>
		public StringEntity(string @string, string charset) : this(@string, ContentType.Create
			(ContentType.TextPlain.GetMimeType(), charset))
		{
		}

		/// <summary>Creates a StringEntity with the specified content and charset.</summary>
		/// <remarks>
		/// Creates a StringEntity with the specified content and charset. The MIME type defaults
		/// to "text/plain".
		/// </remarks>
		/// <param name="string">
		/// content to be used. Not
		/// <code>null</code>
		/// .
		/// </param>
		/// <param name="charset">
		/// character set to be used. May be
		/// <code>null</code>
		/// , in which case the default
		/// is
		/// <see cref="Org.Apache.Http.Protocol.HTTP.DefContentCharset">Org.Apache.Http.Protocol.HTTP.DefContentCharset
		/// 	</see>
		/// is assumed
		/// </param>
		/// <exception cref="System.ArgumentException">if the string parameter is null</exception>
		/// <since>4.2</since>
		public StringEntity(string @string, Encoding charset) : this(@string, ContentType
			.Create(ContentType.TextPlain.GetMimeType(), charset))
		{
		}

		/// <summary>Creates a StringEntity with the specified content.</summary>
		/// <remarks>
		/// Creates a StringEntity with the specified content. The content type defaults to
		/// <see cref="ContentType.TextPlain">ContentType.TextPlain</see>
		/// .
		/// </remarks>
		/// <param name="string">
		/// content to be used. Not
		/// <code>null</code>
		/// .
		/// </param>
		/// <exception cref="System.ArgumentException">if the string parameter is null</exception>
		/// <exception cref="System.IO.UnsupportedEncodingException">if the default HTTP charset is not supported.
		/// 	</exception>
		public StringEntity(string @string) : this(@string, ContentType.DefaultText)
		{
		}

		public override bool IsRepeatable()
		{
			return true;
		}

		public override long GetContentLength()
		{
			return this.content.Length;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override InputStream GetContent()
		{
			return new ByteArrayInputStream(this.content);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void WriteTo(OutputStream outstream)
		{
			Args.NotNull(outstream, "Output stream");
			outstream.Write(this.content);
			outstream.Flush();
		}

		/// <summary>Tells that this entity is not streaming.</summary>
		/// <remarks>Tells that this entity is not streaming.</remarks>
		/// <returns><code>false</code></returns>
		public override bool IsStreaming()
		{
			return false;
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			return base.MemberwiseClone();
		}
	}
}

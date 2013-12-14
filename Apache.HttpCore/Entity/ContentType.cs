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

using System.Globalization;
using System.Text;
using Org.Apache.Http;
using Org.Apache.Http.Message;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Entity
{
	/// <summary>Content type information consisting of a MIME type and an optional charset.
	/// 	</summary>
	/// <remarks>
	/// Content type information consisting of a MIME type and an optional charset.
	/// <p/>
	/// This class makes no attempts to verify validity of the MIME type.
	/// The input parameters of the
	/// <see cref="Create(string, string)">Create(string, string)</see>
	/// method, however, may not
	/// contain characters <">, <;>, <,> reserved by the HTTP specification.
	/// </remarks>
	/// <since>4.2</since>
	[System.Serializable]
	public sealed class ContentType
	{
		private const long serialVersionUID = -7768694718232371896L;

		public static readonly Org.Apache.Http.Entity.ContentType ApplicationAtomXml = Create
			("application/atom+xml", Consts.Iso88591);

		public static readonly Org.Apache.Http.Entity.ContentType ApplicationFormUrlencoded
			 = Create("application/x-www-form-urlencoded", Consts.Iso88591);

		public static readonly Org.Apache.Http.Entity.ContentType ApplicationJson = Create
			("application/json", Consts.Utf8);

		public static readonly Org.Apache.Http.Entity.ContentType ApplicationOctetStream = 
			Create("application/octet-stream", (Encoding)null);

		public static readonly Org.Apache.Http.Entity.ContentType ApplicationSvgXml = Create
			("application/svg+xml", Consts.Iso88591);

		public static readonly Org.Apache.Http.Entity.ContentType ApplicationXhtmlXml = Create
			("application/xhtml+xml", Consts.Iso88591);

		public static readonly Org.Apache.Http.Entity.ContentType ApplicationXml = Create
			("application/xml", Consts.Iso88591);

		public static readonly Org.Apache.Http.Entity.ContentType MultipartFormData = Create
			("multipart/form-data", Consts.Iso88591);

		public static readonly Org.Apache.Http.Entity.ContentType TextHtml = Create("text/html"
			, Consts.Iso88591);

		public static readonly Org.Apache.Http.Entity.ContentType TextPlain = Create("text/plain"
			, Consts.Iso88591);

		public static readonly Org.Apache.Http.Entity.ContentType TextXml = Create("text/xml"
			, Consts.Iso88591);

		public static readonly Org.Apache.Http.Entity.ContentType Wildcard = Create("*/*"
			, (Encoding)null);

		public static readonly Org.Apache.Http.Entity.ContentType DefaultText = TextPlain;

		public static readonly Org.Apache.Http.Entity.ContentType DefaultBinary = ApplicationOctetStream;

		private readonly string mimeType;

		private readonly Encoding charset;

		private readonly NameValuePair[] @params;

		internal ContentType(string mimeType, Encoding charset)
		{
			// constants
			// defaults
			this.mimeType = mimeType;
			this.charset = charset;
			this.@params = null;
		}

		/// <exception cref="Sharpen.UnsupportedCharsetException"></exception>
		internal ContentType(string mimeType, NameValuePair[] @params)
		{
			this.mimeType = mimeType;
			this.@params = @params;
			string s = GetParameter("charset");
			this.charset = !TextUtils.IsBlank(s) ? Sharpen.Extensions.GetEncoding(s) : null;
		}

		public string GetMimeType()
		{
			return this.mimeType;
		}

		public Encoding GetCharset()
		{
			return this.charset;
		}

		/// <since>4.3</since>
		public string GetParameter(string name)
		{
			Args.NotEmpty(name, "Parameter name");
			if (this.@params == null)
			{
				return null;
			}
			foreach (NameValuePair param in this.@params)
			{
				if (Sharpen.Runtime.EqualsIgnoreCase(param.GetName(), name))
				{
					return param.GetValue();
				}
			}
			return null;
		}

		/// <summary>
		/// Generates textual representation of this content type which can be used as the value
		/// of a <code>Content-Type</code> header.
		/// </summary>
		/// <remarks>
		/// Generates textual representation of this content type which can be used as the value
		/// of a <code>Content-Type</code> header.
		/// </remarks>
		public override string ToString()
		{
			CharArrayBuffer buf = new CharArrayBuffer(64);
			buf.Append(this.mimeType);
			if (this.@params != null)
			{
				buf.Append("; ");
				BasicHeaderValueFormatter.Instance.FormatParameters(buf, this.@params, false);
			}
			else
			{
				if (this.charset != null)
				{
					buf.Append("; charset=");
					buf.Append(this.charset.Name());
				}
			}
			return buf.ToString();
		}

		private static bool Valid(string s)
		{
			for (int i = 0; i < s.Length; i++)
			{
				char ch = s[i];
				if (ch == '"' || ch == ',' || ch == ';')
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Creates a new instance of
		/// <see cref="ContentType">ContentType</see>
		/// .
		/// </summary>
		/// <param name="mimeType">
		/// MIME type. It may not be <code>null</code> or empty. It may not contain
		/// characters <">, <;>, <,> reserved by the HTTP specification.
		/// </param>
		/// <param name="charset">charset.</param>
		/// <returns>content type</returns>
		public static Org.Apache.Http.Entity.ContentType Create(string mimeType, Encoding
			 charset)
		{
			string type = Args.NotBlank(mimeType, "MIME type").ToLower(CultureInfo.InvariantCulture
				);
			Args.Check(Valid(type), "MIME type may not contain reserved characters");
			return new Org.Apache.Http.Entity.ContentType(type, charset);
		}

		/// <summary>
		/// Creates a new instance of
		/// <see cref="ContentType">ContentType</see>
		/// without a charset.
		/// </summary>
		/// <param name="mimeType">
		/// MIME type. It may not be <code>null</code> or empty. It may not contain
		/// characters <">, <;>, <,> reserved by the HTTP specification.
		/// </param>
		/// <returns>content type</returns>
		public static Org.Apache.Http.Entity.ContentType Create(string mimeType)
		{
			return new Org.Apache.Http.Entity.ContentType(mimeType, (Encoding)null);
		}

		/// <summary>
		/// Creates a new instance of
		/// <see cref="ContentType">ContentType</see>
		/// .
		/// </summary>
		/// <param name="mimeType">
		/// MIME type. It may not be <code>null</code> or empty. It may not contain
		/// characters <">, <;>, <,> reserved by the HTTP specification.
		/// </param>
		/// <param name="charset">
		/// charset. It may not contain characters <">, <;>, <,> reserved by the HTTP
		/// specification. This parameter is optional.
		/// </param>
		/// <returns>content type</returns>
		/// <exception cref="Sharpen.UnsupportedCharsetException">
		/// Thrown when the named charset is not available in
		/// this instance of the Java virtual machine
		/// </exception>
		public static Org.Apache.Http.Entity.ContentType Create(string mimeType, string charset
			)
		{
			return Create(mimeType, !TextUtils.IsBlank(charset) ? Sharpen.Extensions.GetEncoding
				(charset) : null);
		}

		private static Org.Apache.Http.Entity.ContentType Create(HeaderElement helem)
		{
			string mimeType = helem.GetName();
			NameValuePair[] @params = helem.GetParameters();
			return new Org.Apache.Http.Entity.ContentType(mimeType, @params != null && @params
				.Length > 0 ? @params : null);
		}

		/// <summary>Parses textual representation of <code>Content-Type</code> value.</summary>
		/// <remarks>Parses textual representation of <code>Content-Type</code> value.</remarks>
		/// <param name="s">text</param>
		/// <returns>content type</returns>
		/// <exception cref="Org.Apache.Http.ParseException">
		/// if the given text does not represent a valid
		/// <code>Content-Type</code> value.
		/// </exception>
		/// <exception cref="Sharpen.UnsupportedCharsetException">
		/// Thrown when the named charset is not available in
		/// this instance of the Java virtual machine
		/// </exception>
		public static Org.Apache.Http.Entity.ContentType Parse(string s)
		{
			Args.NotNull(s, "Content type");
			CharArrayBuffer buf = new CharArrayBuffer(s.Length);
			buf.Append(s);
			ParserCursor cursor = new ParserCursor(0, s.Length);
			HeaderElement[] elements = BasicHeaderValueParser.Instance.ParseElements(buf, cursor
				);
			if (elements.Length > 0)
			{
				return Create(elements[0]);
			}
			else
			{
				throw new ParseException("Invalid content type: " + s);
			}
		}

		/// <summary>
		/// Extracts <code>Content-Type</code> value from
		/// <see cref="Org.Apache.Http.HttpEntity">Org.Apache.Http.HttpEntity</see>
		/// exactly as
		/// specified by the <code>Content-Type</code> header of the entity. Returns <code>null</code>
		/// if not specified.
		/// </summary>
		/// <param name="entity">HTTP entity</param>
		/// <returns>content type</returns>
		/// <exception cref="Org.Apache.Http.ParseException">
		/// if the given text does not represent a valid
		/// <code>Content-Type</code> value.
		/// </exception>
		/// <exception cref="Sharpen.UnsupportedCharsetException">
		/// Thrown when the named charset is not available in
		/// this instance of the Java virtual machine
		/// </exception>
		public static Org.Apache.Http.Entity.ContentType Get(HttpEntity entity)
		{
			if (entity == null)
			{
				return null;
			}
			Header header = entity.GetContentType();
			if (header != null)
			{
				HeaderElement[] elements = header.GetElements();
				if (elements.Length > 0)
				{
					return Create(elements[0]);
				}
			}
			return null;
		}

		/// <summary>
		/// Extracts <code>Content-Type</code> value from
		/// <see cref="Org.Apache.Http.HttpEntity">Org.Apache.Http.HttpEntity</see>
		/// or returns the default value
		/// <see cref="DefaultText">DefaultText</see>
		/// if not explicitly specified.
		/// </summary>
		/// <param name="entity">HTTP entity</param>
		/// <returns>content type</returns>
		/// <exception cref="Org.Apache.Http.ParseException">
		/// if the given text does not represent a valid
		/// <code>Content-Type</code> value.
		/// </exception>
		/// <exception cref="Sharpen.UnsupportedCharsetException">
		/// Thrown when the named charset is not available in
		/// this instance of the Java virtual machine
		/// </exception>
		public static Org.Apache.Http.Entity.ContentType GetOrDefault(HttpEntity entity)
		{
			Org.Apache.Http.Entity.ContentType contentType = Get(entity);
			return contentType != null ? contentType : DefaultText;
		}

		/// <summary>Creates a new instance with this MIME type and the given Charset.</summary>
		/// <remarks>Creates a new instance with this MIME type and the given Charset.</remarks>
		/// <param name="charset">charset</param>
		/// <returns>a new instance with this MIME type and the given Charset.</returns>
		/// <since>4.3</since>
		public Org.Apache.Http.Entity.ContentType WithCharset(Encoding charset)
		{
			return Create(this.GetMimeType(), charset);
		}

		/// <summary>Creates a new instance with this MIME type and the given Charset name.</summary>
		/// <remarks>Creates a new instance with this MIME type and the given Charset name.</remarks>
		/// <param name="charset">name</param>
		/// <returns>a new instance with this MIME type and the given Charset name.</returns>
		/// <exception cref="Sharpen.UnsupportedCharsetException">
		/// Thrown when the named charset is not available in
		/// this instance of the Java virtual machine
		/// </exception>
		/// <since>4.3</since>
		public Org.Apache.Http.Entity.ContentType WithCharset(string charset)
		{
			return Create(this.GetMimeType(), charset);
		}
	}
}

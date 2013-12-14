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
using Org.Apache.Http.Message;
using Org.Apache.Http.Protocol;
using Sharpen;

namespace Org.Apache.Http.Entity
{
	/// <summary>Abstract base class for entities.</summary>
	/// <remarks>
	/// Abstract base class for entities.
	/// Provides the commonly used attributes for streamed and self-contained
	/// implementations of
	/// <see cref="Org.Apache.Http.HttpEntity">HttpEntity</see>
	/// .
	/// </remarks>
	/// <since>4.0</since>
	public abstract class AbstractHttpEntity : HttpEntity
	{
		/// <summary>Buffer size for output stream processing.</summary>
		/// <remarks>Buffer size for output stream processing.</remarks>
		/// <since>4.3</since>
		protected internal const int OutputBufferSize = 4096;

		protected internal Header contentType;

		protected internal Header contentEncoding;

		protected internal bool chunked;

		/// <summary>Protected default constructor.</summary>
		/// <remarks>
		/// Protected default constructor.
		/// The contentType, contentEncoding and chunked attributes of the created object are set to
		/// <code>null</code>, <code>null</code> and <code>false</code>, respectively.
		/// </remarks>
		protected internal AbstractHttpEntity() : base()
		{
		}

		/// <summary>Obtains the Content-Type header.</summary>
		/// <remarks>
		/// Obtains the Content-Type header.
		/// The default implementation returns the value of the
		/// <see cref="contentType">contentType</see>
		/// attribute.
		/// </remarks>
		/// <returns>the Content-Type header, or <code>null</code></returns>
		public virtual Header GetContentType()
		{
			return this.contentType;
		}

		/// <summary>Obtains the Content-Encoding header.</summary>
		/// <remarks>
		/// Obtains the Content-Encoding header.
		/// The default implementation returns the value of the
		/// <see cref="contentEncoding">contentEncoding</see>
		/// attribute.
		/// </remarks>
		/// <returns>the Content-Encoding header, or <code>null</code></returns>
		public virtual Header GetContentEncoding()
		{
			return this.contentEncoding;
		}

		/// <summary>Obtains the 'chunked' flag.</summary>
		/// <remarks>
		/// Obtains the 'chunked' flag.
		/// The default implementation returns the value of the
		/// <see cref="chunked">chunked</see>
		/// attribute.
		/// </remarks>
		/// <returns>the 'chunked' flag</returns>
		public virtual bool IsChunked()
		{
			return this.chunked;
		}

		/// <summary>Specifies the Content-Type header.</summary>
		/// <remarks>
		/// Specifies the Content-Type header.
		/// The default implementation sets the value of the
		/// <see cref="contentType">contentType</see>
		/// attribute.
		/// </remarks>
		/// <param name="contentType">
		/// the new Content-Encoding header, or
		/// <code>null</code> to unset
		/// </param>
		public virtual void SetContentType(Header contentType)
		{
			this.contentType = contentType;
		}

		/// <summary>Specifies the Content-Type header, as a string.</summary>
		/// <remarks>
		/// Specifies the Content-Type header, as a string.
		/// The default implementation calls
		/// <see cref="SetContentType(Org.Apache.Http.Header)">setContentType(Header)</see>
		/// .
		/// </remarks>
		/// <param name="ctString">
		/// the new Content-Type header, or
		/// <code>null</code> to unset
		/// </param>
		public virtual void SetContentType(string ctString)
		{
			Header h = null;
			if (ctString != null)
			{
				h = new BasicHeader(HTTP.ContentType, ctString);
			}
			SetContentType(h);
		}

		/// <summary>Specifies the Content-Encoding header.</summary>
		/// <remarks>
		/// Specifies the Content-Encoding header.
		/// The default implementation sets the value of the
		/// <see cref="contentEncoding">contentEncoding</see>
		/// attribute.
		/// </remarks>
		/// <param name="contentEncoding">
		/// the new Content-Encoding header, or
		/// <code>null</code> to unset
		/// </param>
		public virtual void SetContentEncoding(Header contentEncoding)
		{
			this.contentEncoding = contentEncoding;
		}

		/// <summary>Specifies the Content-Encoding header, as a string.</summary>
		/// <remarks>
		/// Specifies the Content-Encoding header, as a string.
		/// The default implementation calls
		/// <see cref="SetContentEncoding(Org.Apache.Http.Header)">setContentEncoding(Header)
		/// 	</see>
		/// .
		/// </remarks>
		/// <param name="ceString">
		/// the new Content-Encoding header, or
		/// <code>null</code> to unset
		/// </param>
		public virtual void SetContentEncoding(string ceString)
		{
			Header h = null;
			if (ceString != null)
			{
				h = new BasicHeader(HTTP.ContentEncoding, ceString);
			}
			SetContentEncoding(h);
		}

		/// <summary>Specifies the 'chunked' flag.</summary>
		/// <remarks>
		/// Specifies the 'chunked' flag.
		/// <p>
		/// Note that the chunked setting is a hint only.
		/// If using HTTP/1.0, chunking is never performed.
		/// Otherwise, even if chunked is false, HttpClient must
		/// use chunk coding if the entity content length is
		/// unknown (-1).
		/// <p>
		/// The default implementation sets the value of the
		/// <see cref="chunked">chunked</see>
		/// attribute.
		/// </remarks>
		/// <param name="b">the new 'chunked' flag</param>
		public virtual void SetChunked(bool b)
		{
			this.chunked = b;
		}

		/// <summary>The default implementation does not consume anything.</summary>
		/// <remarks>The default implementation does not consume anything.</remarks>
		/// <exception cref="System.IO.IOException"></exception>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) Either use Org.Apache.Http.HttpEntity.GetContent() and call System.IO.InputStream.Close() on that; otherwise call Org.Apache.Http.HttpEntity.WriteTo(System.IO.OutputStream) which is required to free the resources."
			)]
		public virtual void ConsumeContent()
		{
		}

		public abstract InputStream GetContent();

		public abstract long GetContentLength();

		public abstract bool IsRepeatable();

		public abstract bool IsStreaming();

		public abstract void WriteTo(OutputStream arg1);
	}
}

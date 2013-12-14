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
using Org.Apache.Http;
using Org.Apache.Http.Message;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>
	/// Basic implementation of
	/// <see cref="Org.Apache.Http.HttpResponse">Org.Apache.Http.HttpResponse</see>
	/// .
	/// </summary>
	/// <seealso cref="Org.Apache.Http.Impl.DefaultHttpResponseFactory">Org.Apache.Http.Impl.DefaultHttpResponseFactory
	/// 	</seealso>
	/// <since>4.0</since>
	public class BasicHttpResponse : AbstractHttpMessage, HttpResponse
	{
		private StatusLine statusline;

		private ProtocolVersion ver;

		private int code;

		private string reasonPhrase;

		private HttpEntity entity;

		private readonly ReasonPhraseCatalog reasonCatalog;

		private CultureInfo locale;

		/// <summary>Creates a new response.</summary>
		/// <remarks>
		/// Creates a new response.
		/// This is the constructor to which all others map.
		/// </remarks>
		/// <param name="statusline">the status line</param>
		/// <param name="catalog">
		/// the reason phrase catalog, or
		/// <code>null</code> to disable automatic
		/// reason phrase lookup
		/// </param>
		/// <param name="locale">
		/// the locale for looking up reason phrases, or
		/// <code>null</code> for the system locale
		/// </param>
		public BasicHttpResponse(StatusLine statusline, ReasonPhraseCatalog catalog, CultureInfo
			 locale) : base()
		{
			this.statusline = Args.NotNull(statusline, "Status line");
			this.ver = statusline.GetProtocolVersion();
			this.code = statusline.GetStatusCode();
			this.reasonPhrase = statusline.GetReasonPhrase();
			this.reasonCatalog = catalog;
			this.locale = locale;
		}

		/// <summary>Creates a response from a status line.</summary>
		/// <remarks>
		/// Creates a response from a status line.
		/// The response will not have a reason phrase catalog and
		/// use the system default locale.
		/// </remarks>
		/// <param name="statusline">the status line</param>
		public BasicHttpResponse(StatusLine statusline) : base()
		{
			this.statusline = Args.NotNull(statusline, "Status line");
			this.ver = statusline.GetProtocolVersion();
			this.code = statusline.GetStatusCode();
			this.reasonPhrase = statusline.GetReasonPhrase();
			this.reasonCatalog = null;
			this.locale = null;
		}

		/// <summary>Creates a response from elements of a status line.</summary>
		/// <remarks>
		/// Creates a response from elements of a status line.
		/// The response will not have a reason phrase catalog and
		/// use the system default locale.
		/// </remarks>
		/// <param name="ver">the protocol version of the response</param>
		/// <param name="code">the status code of the response</param>
		/// <param name="reason">
		/// the reason phrase to the status code, or
		/// <code>null</code>
		/// </param>
		public BasicHttpResponse(ProtocolVersion ver, int code, string reason) : base()
		{
			Args.NotNegative(code, "Status code");
			this.statusline = null;
			this.ver = ver;
			this.code = code;
			this.reasonPhrase = reason;
			this.reasonCatalog = null;
			this.locale = null;
		}

		// non-javadoc, see interface HttpMessage
		public override ProtocolVersion GetProtocolVersion()
		{
			return this.ver;
		}

		// non-javadoc, see interface HttpResponse
		public virtual StatusLine GetStatusLine()
		{
			if (this.statusline == null)
			{
				this.statusline = new BasicStatusLine(this.ver != null ? this.ver : HttpVersion.Http11
					, this.code, this.reasonPhrase != null ? this.reasonPhrase : GetReason(this.code
					));
			}
			return this.statusline;
		}

		// non-javadoc, see interface HttpResponse
		public virtual HttpEntity GetEntity()
		{
			return this.entity;
		}

		public virtual CultureInfo GetLocale()
		{
			return this.locale;
		}

		// non-javadoc, see interface HttpResponse
		public virtual void SetStatusLine(StatusLine statusline)
		{
			this.statusline = Args.NotNull(statusline, "Status line");
			this.ver = statusline.GetProtocolVersion();
			this.code = statusline.GetStatusCode();
			this.reasonPhrase = statusline.GetReasonPhrase();
		}

		// non-javadoc, see interface HttpResponse
		public virtual void SetStatusLine(ProtocolVersion ver, int code)
		{
			Args.NotNegative(code, "Status code");
			this.statusline = null;
			this.ver = ver;
			this.code = code;
			this.reasonPhrase = null;
		}

		// non-javadoc, see interface HttpResponse
		public virtual void SetStatusLine(ProtocolVersion ver, int code, string reason)
		{
			Args.NotNegative(code, "Status code");
			this.statusline = null;
			this.ver = ver;
			this.code = code;
			this.reasonPhrase = reason;
		}

		// non-javadoc, see interface HttpResponse
		public virtual void SetStatusCode(int code)
		{
			Args.NotNegative(code, "Status code");
			this.statusline = null;
			this.code = code;
			this.reasonPhrase = null;
		}

		// non-javadoc, see interface HttpResponse
		public virtual void SetReasonPhrase(string reason)
		{
			this.statusline = null;
			this.reasonPhrase = reason;
		}

		// non-javadoc, see interface HttpResponse
		public virtual void SetEntity(HttpEntity entity)
		{
			this.entity = entity;
		}

		public virtual void SetLocale(CultureInfo locale)
		{
			this.locale = Args.NotNull(locale, "Locale");
			this.statusline = null;
		}

		/// <summary>Looks up a reason phrase.</summary>
		/// <remarks>
		/// Looks up a reason phrase.
		/// This method evaluates the currently set catalog and locale.
		/// It also handles a missing catalog.
		/// </remarks>
		/// <param name="code">the status code for which to look up the reason</param>
		/// <returns>the reason phrase, or <code>null</code> if there is none</returns>
		protected internal virtual string GetReason(int code)
		{
			return this.reasonCatalog != null ? this.reasonCatalog.GetReason(code, this.locale
				 != null ? this.locale : CultureInfo.CurrentCulture) : null;
		}

		public override string ToString()
		{
			return GetStatusLine() + " " + this.headergroup;
		}
	}
}

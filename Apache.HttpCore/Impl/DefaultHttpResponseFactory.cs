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
using Org.Apache.Http.Impl;
using Org.Apache.Http.Message;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl
{
	/// <summary>
	/// Default factory for creating
	/// <see cref="Org.Apache.Http.HttpResponse">Org.Apache.Http.HttpResponse</see>
	/// objects.
	/// </summary>
	/// <since>4.0</since>
	public class DefaultHttpResponseFactory : HttpResponseFactory
	{
		public static readonly Org.Apache.Http.Impl.DefaultHttpResponseFactory Instance = 
			new Org.Apache.Http.Impl.DefaultHttpResponseFactory();

		/// <summary>The catalog for looking up reason phrases.</summary>
		/// <remarks>The catalog for looking up reason phrases.</remarks>
		protected internal readonly ReasonPhraseCatalog reasonCatalog;

		/// <summary>Creates a new response factory with the given catalog.</summary>
		/// <remarks>Creates a new response factory with the given catalog.</remarks>
		/// <param name="catalog">the catalog of reason phrases</param>
		public DefaultHttpResponseFactory(ReasonPhraseCatalog catalog)
		{
			this.reasonCatalog = Args.NotNull(catalog, "Reason phrase catalog");
		}

		/// <summary>Creates a new response factory with the default catalog.</summary>
		/// <remarks>
		/// Creates a new response factory with the default catalog.
		/// The default catalog is
		/// <see cref="EnglishReasonPhraseCatalog">EnglishReasonPhraseCatalog</see>
		/// .
		/// </remarks>
		public DefaultHttpResponseFactory() : this(EnglishReasonPhraseCatalog.Instance)
		{
		}

		// non-javadoc, see interface HttpResponseFactory
		public virtual HttpResponse NewHttpResponse(ProtocolVersion ver, int status, HttpContext
			 context)
		{
			Args.NotNull(ver, "HTTP version");
			CultureInfo loc = DetermineLocale(context);
			string reason = this.reasonCatalog.GetReason(status, loc);
			StatusLine statusline = new BasicStatusLine(ver, status, reason);
			return new BasicHttpResponse(statusline, this.reasonCatalog, loc);
		}

		// non-javadoc, see interface HttpResponseFactory
		public virtual HttpResponse NewHttpResponse(StatusLine statusline, HttpContext context
			)
		{
			Args.NotNull(statusline, "Status line");
			return new BasicHttpResponse(statusline, this.reasonCatalog, DetermineLocale(context
				));
		}

		/// <summary>Determines the locale of the response.</summary>
		/// <remarks>
		/// Determines the locale of the response.
		/// The implementation in this class always returns the default locale.
		/// </remarks>
		/// <param name="context">
		/// the context from which to determine the locale, or
		/// <code>null</code> to use the default locale
		/// </param>
		/// <returns>the locale for the response, never <code>null</code></returns>
		protected internal virtual CultureInfo DetermineLocale(HttpContext context)
		{
			return CultureInfo.CurrentCulture;
		}
	}
}

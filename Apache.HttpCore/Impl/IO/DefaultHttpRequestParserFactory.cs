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

using Org.Apache.Http;
using Org.Apache.Http.Config;
using Org.Apache.Http.IO;
using Org.Apache.Http.Impl;
using Org.Apache.Http.Impl.IO;
using Org.Apache.Http.Message;
using Sharpen;

namespace Org.Apache.Http.Impl.IO
{
	/// <summary>Default factory for request message parsers.</summary>
	/// <remarks>Default factory for request message parsers.</remarks>
	/// <since>4.3</since>
	public class DefaultHttpRequestParserFactory : HttpMessageParserFactory<IHttpRequest
		>
	{
		public static readonly Org.Apache.Http.Impl.IO.DefaultHttpRequestParserFactory Instance
			 = new Org.Apache.Http.Impl.IO.DefaultHttpRequestParserFactory();

		private readonly LineParser lineParser;

		private readonly HttpRequestFactory requestFactory;

		public DefaultHttpRequestParserFactory(LineParser lineParser, HttpRequestFactory 
			requestFactory) : base()
		{
			this.lineParser = lineParser != null ? lineParser : BasicLineParser.Instance;
			this.requestFactory = requestFactory != null ? requestFactory : DefaultHttpRequestFactory
				.Instance;
		}

		public DefaultHttpRequestParserFactory() : this(null, null)
		{
		}

		public virtual HttpMessageParser<IHttpRequest> Create(SessionInputBuffer buffer, 
			MessageConstraints constraints)
		{
			return new DefaultHttpRequestParser(buffer, lineParser, requestFactory, constraints
				);
		}
	}
}

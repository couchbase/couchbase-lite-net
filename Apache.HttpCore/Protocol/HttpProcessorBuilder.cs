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
using Org.Apache.Http.Protocol;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>
	/// Builder for
	/// <see cref="HttpProcessor">HttpProcessor</see>
	/// instances.
	/// </summary>
	/// <since>4.3</since>
	public class HttpProcessorBuilder
	{
		private ChainBuilder<IHttpRequestInterceptor> requestChainBuilder;

		private ChainBuilder<HttpResponseInterceptor> responseChainBuilder;

		public static Org.Apache.Http.Protocol.HttpProcessorBuilder Create()
		{
			return new Org.Apache.Http.Protocol.HttpProcessorBuilder();
		}

		internal HttpProcessorBuilder() : base()
		{
		}

		private ChainBuilder<IHttpRequestInterceptor> GetRequestChainBuilder()
		{
			if (requestChainBuilder == null)
			{
				requestChainBuilder = new ChainBuilder<IHttpRequestInterceptor>();
			}
			return requestChainBuilder;
		}

		private ChainBuilder<HttpResponseInterceptor> GetResponseChainBuilder()
		{
			if (responseChainBuilder == null)
			{
				responseChainBuilder = new ChainBuilder<HttpResponseInterceptor>();
			}
			return responseChainBuilder;
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder AddFirst(IHttpRequestInterceptor
			 e)
		{
			if (e == null)
			{
				return this;
			}
			GetRequestChainBuilder().AddFirst(e);
			return this;
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder AddLast(IHttpRequestInterceptor
			 e)
		{
			if (e == null)
			{
				return this;
			}
			GetRequestChainBuilder().AddLast(e);
			return this;
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder Add(IHttpRequestInterceptor
			 e)
		{
			return AddLast(e);
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder AddAllFirst(params IHttpRequestInterceptor
			[] e)
		{
			if (e == null)
			{
				return this;
			}
			GetRequestChainBuilder().AddAllFirst(e);
			return this;
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder AddAllLast(params IHttpRequestInterceptor
			[] e)
		{
			if (e == null)
			{
				return this;
			}
			GetRequestChainBuilder().AddAllLast(e);
			return this;
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder AddAll(params IHttpRequestInterceptor
			[] e)
		{
			return AddAllLast(e);
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder AddFirst(HttpResponseInterceptor
			 e)
		{
			if (e == null)
			{
				return this;
			}
			GetResponseChainBuilder().AddFirst(e);
			return this;
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder AddLast(HttpResponseInterceptor
			 e)
		{
			if (e == null)
			{
				return this;
			}
			GetResponseChainBuilder().AddLast(e);
			return this;
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder Add(HttpResponseInterceptor
			 e)
		{
			return AddLast(e);
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder AddAllFirst(params HttpResponseInterceptor
			[] e)
		{
			if (e == null)
			{
				return this;
			}
			GetResponseChainBuilder().AddAllFirst(e);
			return this;
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder AddAllLast(params HttpResponseInterceptor
			[] e)
		{
			if (e == null)
			{
				return this;
			}
			GetResponseChainBuilder().AddAllLast(e);
			return this;
		}

		public virtual Org.Apache.Http.Protocol.HttpProcessorBuilder AddAll(params HttpResponseInterceptor
			[] e)
		{
			return AddAllLast(e);
		}

		public virtual HttpProcessor Build()
		{
			return new ImmutableHttpProcessor(requestChainBuilder != null ? requestChainBuilder
				.Build() : null, responseChainBuilder != null ? responseChainBuilder.Build() : null
				);
		}
	}
}

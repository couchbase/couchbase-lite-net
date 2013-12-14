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
using Org.Apache.Http;
using Org.Apache.Http.Protocol;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>
	/// Immutable
	/// <see cref="HttpProcessor">HttpProcessor</see>
	/// .
	/// </summary>
	/// <since>4.1</since>
	public sealed class ImmutableHttpProcessor : HttpProcessor
	{
		private readonly IHttpRequestInterceptor[] requestInterceptors;

		private readonly HttpResponseInterceptor[] responseInterceptors;

		public ImmutableHttpProcessor(IHttpRequestInterceptor[] requestInterceptors, HttpResponseInterceptor
			[] responseInterceptors) : base()
		{
			// provided injected dependencies are immutable
			if (requestInterceptors != null)
			{
				int l = requestInterceptors.Length;
				this.requestInterceptors = new IHttpRequestInterceptor[l];
				System.Array.Copy(requestInterceptors, 0, this.requestInterceptors, 0, l);
			}
			else
			{
				this.requestInterceptors = new IHttpRequestInterceptor[0];
			}
			if (responseInterceptors != null)
			{
				int l = responseInterceptors.Length;
				this.responseInterceptors = new HttpResponseInterceptor[l];
				System.Array.Copy(responseInterceptors, 0, this.responseInterceptors, 0, l);
			}
			else
			{
				this.responseInterceptors = new HttpResponseInterceptor[0];
			}
		}

		/// <since>4.3</since>
		public ImmutableHttpProcessor(IList<IHttpRequestInterceptor> requestInterceptors, 
			IList<HttpResponseInterceptor> responseInterceptors) : base()
		{
			if (requestInterceptors != null)
			{
				int l = requestInterceptors.Count;
				this.requestInterceptors = Sharpen.Collections.ToArray(requestInterceptors, new IHttpRequestInterceptor
					[l]);
			}
			else
			{
				this.requestInterceptors = new IHttpRequestInterceptor[0];
			}
			if (responseInterceptors != null)
			{
				int l = responseInterceptors.Count;
				this.responseInterceptors = Sharpen.Collections.ToArray(responseInterceptors, new 
					HttpResponseInterceptor[l]);
			}
			else
			{
				this.responseInterceptors = new HttpResponseInterceptor[0];
			}
		}

//		[Obsolete]
//		[System.ObsoleteAttribute(@"(4.3) do not use.")]
//		public ImmutableHttpProcessor(HttpRequestInterceptorList requestInterceptors, HttpResponseInterceptorList
//			 responseInterceptors) : base()
//		{
//			if (requestInterceptors != null)
//			{
//				int count = requestInterceptors.GetRequestInterceptorCount();
//				this.requestInterceptors = new IHttpRequestInterceptor[count];
//				for (int i = 0; i < count; i++)
//				{
//					this.requestInterceptors[i] = requestInterceptors.GetRequestInterceptor(i);
//				}
//			}
//			else
//			{
//				this.requestInterceptors = new IHttpRequestInterceptor[0];
//			}
//			if (responseInterceptors != null)
//			{
//				int count = responseInterceptors.GetResponseInterceptorCount();
//				this.responseInterceptors = new HttpResponseInterceptor[count];
//				for (int i = 0; i < count; i++)
//				{
//					this.responseInterceptors[i] = responseInterceptors.GetResponseInterceptor(i);
//				}
//			}
//			else
//			{
//				this.responseInterceptors = new HttpResponseInterceptor[0];
//			}
//		}

		public ImmutableHttpProcessor(params IHttpRequestInterceptor[] requestInterceptors
			) : this(requestInterceptors, null)
		{
		}

		public ImmutableHttpProcessor(params HttpResponseInterceptor[] responseInterceptors
			) : this(null, responseInterceptors)
		{
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		public void Process(IHttpRequest request, HttpContext context)
		{
			foreach (IHttpRequestInterceptor requestInterceptor in this.requestInterceptors)
			{
				requestInterceptor.Process(request, context);
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		public void Process(HttpResponse response, HttpContext context)
		{
			foreach (HttpResponseInterceptor responseInterceptor in this.responseInterceptors)
			{
				responseInterceptor.Process(response, context);
			}
		}
	}
}

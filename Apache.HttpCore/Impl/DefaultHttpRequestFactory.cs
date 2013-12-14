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
using Org.Apache.Http.Message;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl
{
	/// <summary>
	/// Default factory for creating
	/// <see cref="Org.Apache.Http.IHttpRequest">Org.Apache.Http.IHttpRequest</see>
	/// objects.
	/// </summary>
	/// <since>4.0</since>
	public class DefaultHttpRequestFactory : HttpRequestFactory
	{
		public static readonly Org.Apache.Http.Impl.DefaultHttpRequestFactory Instance = 
			new Org.Apache.Http.Impl.DefaultHttpRequestFactory();

		private static readonly string[] Rfc2616CommonMethods = new string[] { "GET" };

		private static readonly string[] Rfc2616EntityEncMethods = new string[] { "POST", 
			"PUT" };

		private static readonly string[] Rfc2616SpecialMethods = new string[] { "HEAD", "OPTIONS"
			, "DELETE", "TRACE", "CONNECT" };

		public DefaultHttpRequestFactory() : base()
		{
		}

		private static bool IsOneOf(string[] methods, string method)
		{
			foreach (string method2 in methods)
			{
				if (Sharpen.Runtime.EqualsIgnoreCase(method2, method))
				{
					return true;
				}
			}
			return false;
		}

		/// <exception cref="Org.Apache.Http.MethodNotSupportedException"></exception>
		public virtual IHttpRequest NewHttpRequest(RequestLine requestline)
		{
			Args.NotNull(requestline, "Request line");
			string method = requestline.GetMethod();
			if (IsOneOf(Rfc2616CommonMethods, method))
			{
				return new BasicHttpRequest(requestline);
			}
			else
			{
				if (IsOneOf(Rfc2616EntityEncMethods, method))
				{
					return new BasicHttpEntityEnclosingRequest(requestline);
				}
				else
				{
					if (IsOneOf(Rfc2616SpecialMethods, method))
					{
						return new BasicHttpRequest(requestline);
					}
					else
					{
						throw new MethodNotSupportedException(method + " method not supported");
					}
				}
			}
		}

		/// <exception cref="Org.Apache.Http.MethodNotSupportedException"></exception>
		public virtual IHttpRequest NewHttpRequest(string method, string uri)
		{
			if (IsOneOf(Rfc2616CommonMethods, method))
			{
				return new BasicHttpRequest(method, uri);
			}
			else
			{
				if (IsOneOf(Rfc2616EntityEncMethods, method))
				{
					return new BasicHttpEntityEnclosingRequest(method, uri);
				}
				else
				{
					if (IsOneOf(Rfc2616SpecialMethods, method))
					{
						return new BasicHttpRequest(method, uri);
					}
					else
					{
						throw new MethodNotSupportedException(method + " method not supported");
					}
				}
			}
		}
	}
}

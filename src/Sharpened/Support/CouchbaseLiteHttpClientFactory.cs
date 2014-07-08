// 
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//using System.Collections.Generic;
using Apache.Http.Client;
using Apache.Http.Conn;
using Apache.Http.Conn.Scheme;
using Apache.Http.Conn.Ssl;
using Apache.Http.Impl.Client;
using Apache.Http.Impl.Conn.Tsccm;
using Apache.Http.Params;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Support;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class CouchbaseLiteHttpClientFactory : HttpClientFactory
	{
		private CookieStore cookieStore;

		private SSLSocketFactory sslSocketFactory;

		private BasicHttpParams basicHttpParams;

		public const int DefaultConnectionTimeoutSeconds = 60;

		public const int DefaultSoTimeoutSeconds = 60;

		/// <summary>Constructor</summary>
		public CouchbaseLiteHttpClientFactory(CookieStore cookieStore)
		{
			this.cookieStore = cookieStore;
		}

		/// <param name="sslSocketFactoryFromUser">
		/// This is to open up the system for end user to inject the sslSocket factories with their
		/// custom KeyStore
		/// </param>
		[InterfaceAudience.Private]
		public virtual void SetSSLSocketFactory(SSLSocketFactory sslSocketFactoryFromUser
			)
		{
			if (sslSocketFactory != null)
			{
				throw new RuntimeException("SSLSocketFactory already set");
			}
			sslSocketFactory = sslSocketFactoryFromUser;
		}

		[InterfaceAudience.Private]
		public virtual void SetBasicHttpParams(BasicHttpParams basicHttpParams)
		{
			this.basicHttpParams = basicHttpParams;
		}

		[InterfaceAudience.Private]
		public virtual HttpClient GetHttpClient()
		{
			// workaround attempt for issue #81
			// it does not seem like _not_ using the ThreadSafeClientConnManager actually
			// caused any problems, but it seems wise to use it "just in case", since it provides
			// extra safety and there are no observed side effects.
			if (basicHttpParams == null)
			{
				basicHttpParams = new BasicHttpParams();
				HttpConnectionParams.SetConnectionTimeout(basicHttpParams, DefaultConnectionTimeoutSeconds
					 * 1000);
				HttpConnectionParams.SetSoTimeout(basicHttpParams, DefaultSoTimeoutSeconds * 1000
					);
			}
			SchemeRegistry schemeRegistry = new SchemeRegistry();
			schemeRegistry.Register(new Apache.Http.Conn.Scheme.Scheme("http", PlainSocketFactory
				.GetSocketFactory(), 80));
			SSLSocketFactory sslSocketFactory = SSLSocketFactory.GetSocketFactory();
			schemeRegistry.Register(new Apache.Http.Conn.Scheme.Scheme("https", this.sslSocketFactory
				 == null ? sslSocketFactory : this.sslSocketFactory, 443));
			ClientConnectionManager cm = new ThreadSafeClientConnManager(basicHttpParams, schemeRegistry
				);
			DefaultHttpClient client = new DefaultHttpClient(cm, basicHttpParams);
			// synchronize access to the cookieStore in case there is another
			// thread in the middle of updating it.  wait until they are done so we get their changes.
			lock (this)
			{
				client.SetCookieStore(cookieStore);
			}
			return client;
		}

		[InterfaceAudience.Private]
		public virtual void AddCookies(IList<Apache.Http.Cookie.Cookie> cookies)
		{
			if (cookieStore == null)
			{
				return;
			}
			lock (this)
			{
				foreach (Apache.Http.Cookie.Cookie cookie in cookies)
				{
					cookieStore.AddCookie(cookie);
				}
			}
		}

		public virtual void DeleteCookie(string name)
		{
			// since CookieStore does not have a way to delete an individual cookie, do workaround:
			// 1. get all cookies
			// 2. filter list to strip out the one we want to delete
			// 3. clear cookie store
			// 4. re-add all cookies except the one we want to delete
			if (cookieStore == null)
			{
				return;
			}
			IList<Apache.Http.Cookie.Cookie> cookies = cookieStore.GetCookies();
			IList<Apache.Http.Cookie.Cookie> retainedCookies = new AList<Apache.Http.Cookie.Cookie
				>();
			foreach (Apache.Http.Cookie.Cookie cookie in cookies)
			{
				if (!cookie.GetName().Equals(name))
				{
					retainedCookies.AddItem(cookie);
				}
			}
			cookieStore.Clear();
			foreach (Apache.Http.Cookie.Cookie retainedCookie in retainedCookies)
			{
				cookieStore.AddCookie(retainedCookie);
			}
		}

		[InterfaceAudience.Private]
		public virtual CookieStore GetCookieStore()
		{
			return cookieStore;
		}
	}
}

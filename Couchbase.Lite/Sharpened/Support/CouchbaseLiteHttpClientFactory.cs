//
// CouchbaseLiteHttpClientFactory.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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

using System.Collections.Generic;
using Couchbase.Lite.Support;
using Org.Apache.Http.Client;
using Org.Apache.Http.Conn;
using Org.Apache.Http.Conn.Scheme;
using Org.Apache.Http.Conn.Ssl;
using Org.Apache.Http.Impl.Client;
using Org.Apache.Http.Impl.Client.Trunk;
using Org.Apache.Http.Impl.Conn.Tsccm;
using Org.Apache.Http.Params;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class CouchbaseLiteHttpClientFactory : HttpClientFactory
	{
		public static CouchbaseLiteHttpClientFactory Instance;

		private CookieStore cookieStore;

		private SSLSocketFactory sslSocketFactory;

		/// <param name="sslSocketFactoryFromUser">
		/// This is to open up the system for end user to inject the sslSocket factories with their
		/// custom KeyStore
		/// </param>
		public virtual void SetSSLSocketFactory(SSLSocketFactory sslSocketFactoryFromUser
			)
		{
			if (sslSocketFactory != null)
			{
				throw new RuntimeException("SSLSocketFactory already set");
			}
			sslSocketFactory = sslSocketFactoryFromUser;
		}

		public virtual HttpClient GetHttpClient()
		{
			// workaround attempt for issue #81
			// it does not seem like _not_ using the ThreadSafeClientConnManager actually
			// caused any problems, but it seems wise to use it "just in case", since it provides
			// extra safety and there are no observed side effects.
			BasicHttpParams @params = new BasicHttpParams();
			SchemeRegistry schemeRegistry = new SchemeRegistry();
			schemeRegistry.Register(new Org.Apache.Http.Conn.Scheme.Scheme("http", PlainSocketFactory
				.GetSocketFactory(), 80));
			SSLSocketFactory sslSocketFactory = SSLSocketFactory.GetSocketFactory();
			schemeRegistry.Register(new Org.Apache.Http.Conn.Scheme.Scheme("https", this.sslSocketFactory
				 == null ? sslSocketFactory : this.sslSocketFactory, 443));
			ClientConnectionManager cm = new ThreadSafeClientConnManager(@params, schemeRegistry
				);
			DefaultHttpClient client = new DefaultHttpClient(cm, @params);
			// synchronize access to the cookieStore in case there is another
			// thread in the middle of updating it.  wait until they are done so we get their changes.
			lock (this)
			{
				client.SetCookieStore(cookieStore);
			}
			return client;
		}

		public virtual void AddCookies(IList<Org.Apache.Http.Cookie.Cookie> cookies)
		{
			lock (this)
			{
				if (cookieStore == null)
				{
					cookieStore = new BasicCookieStore();
				}
				foreach (Org.Apache.Http.Cookie.Cookie cookie in cookies)
				{
					cookieStore.AddCookie(cookie);
				}
			}
		}
	}
}

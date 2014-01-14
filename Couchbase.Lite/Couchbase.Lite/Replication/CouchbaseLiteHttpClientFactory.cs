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

using Couchbase.Lite.Support;
using System.Net.Http;
using System.Net;
using System;
using Couchbase.Lite.Replicator;

namespace Couchbase.Lite.Support
{
    public class CouchbaseLiteHttpClientFactory : IHttpClientFactory
	{
        public static CouchbaseLiteHttpClientFactory Instance;

        static CouchbaseLiteHttpClientFactory()
        {
            Instance = new CouchbaseLiteHttpClientFactory();
        }

        private readonly CookieContainer cookieStore;
        private readonly Object locker = new Object ();
        private HttpClientHandler handler;

        public CouchbaseLiteHttpClientFactory()
        {
            cookieStore = new CookieContainer ();
        }

		public HttpClient GetHttpClient()
		{
            // Build a pipeline of HttpMessageHandlers.
            handler = new HttpClientHandler 
            {
                CookieContainer = cookieStore
            };

            // NOTE: Probably could set httpHandler.MaxRequestContentBufferSize to Couchbase Lite 
            // max doc size (~16 MB) plus some overhead.
            var client = HttpClientFactory.Create(handler, new DefaultAuthHandler(handler));
            return client;
		}

        public HttpClientHandler HttpHandler {
            get {
                return handler;
            }
        }

        public void AddCookies(CookieCollection cookies)
		{
            lock (locker) {
                cookieStore.Add(cookies);
            }
		}
	}
}
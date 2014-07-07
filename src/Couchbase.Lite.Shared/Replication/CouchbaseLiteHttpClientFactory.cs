//
// CouchbaseLiteHttpClientFactory.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
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
//

using Couchbase.Lite.Support;
using System.Net.Http;
using System.Net;
using System;
using Couchbase.Lite.Replicator;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Couchbase.Lite.Util;
using System.Runtime.Serialization.Formatters.Binary;

namespace Couchbase.Lite.Support
{
    public class CouchbaseLiteHttpClientFactory : IHttpClientFactory
	{
        const string Tag = "CouchbaseLiteHttpClientFactory";

        private readonly CookieStore cookieStore;
        private readonly Object locker = new Object();
        private HttpClientHandler handler;

        public CouchbaseLiteHttpClientFactory(CookieStore cookieStore)
        {
            this.cookieStore = cookieStore;
            Headers = new ConcurrentDictionary<string,string>();
        }

		public HttpClient GetHttpClient()
		{
            // Build a pipeline of HttpMessageHandlers.
            var clientHandler = new HttpClientHandler 
            {
                CookieContainer = cookieStore,
                UseCookies = true,
                UseDefaultCredentials = true
            };

            // NOTE: Probably could set httpHandler.MaxRequestContentBufferSize to Couchbase Lite 
            // max doc size (~16 MB) plus some overhead.
            var authHandler = new DefaultAuthHandler(clientHandler, cookieStore);
            var client =  new HttpClient(authHandler);
            foreach(var header in Headers)
            {
                var success = client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                if (!success)
                    Util.Log.W(Tag, "Unabled to add header to request: {0}: {1}".Fmt(header.Key, header.Value));
            }

            return client;
		}

        public HttpClientHandler HttpHandler {
            get 
            {
                return handler;
            }
        }

        public IDictionary<string, string> Headers { get; set; }

        public void AddCookies(CookieCollection cookies)
        {
            cookieStore.Add(cookies);
        }

        public void DeleteCookie(Uri uri, string name)
        {
            cookieStore.Delete(uri, name);
        }

        public CookieContainer GetCookieContainer()
        {
            return cookieStore;
        }
	}
}

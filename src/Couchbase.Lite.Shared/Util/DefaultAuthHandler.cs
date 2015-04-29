//
// DefaultAuthHandler.cs
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

using System;
using System.Collections;
using System.Net;
using System.Net.Http;
using System.Threading;
using Couchbase.Lite.Util;
using System.Collections.Generic;

namespace Couchbase.Lite.Replicator
{

    internal sealed class DefaultAuthHandler : MessageProcessingHandler
    {
        public DefaultAuthHandler(HttpClientHandler context, CookieStore cookieStore) : base()
        {
            this.context = context;
            this.cookieStore = cookieStore;
            InnerHandler = this.context;
        }

        #region implemented abstract members of MessageProcessingHandler

        object locker = new object();

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content != null) {
                var mre = new ManualResetEvent(false);
                response.Content.LoadIntoBufferAsync().ContinueWith(t => mre.Set());
                if (mre.WaitOne (Manager.DefaultOptions.RequestTimeout, true) == false) {
                    Log.E ("DefaultAuthHandler", "mre.WaitOne timed out");
                }
            }

            var hasSetCookie = response.Headers.Contains("Set-Cookie");
            if (hasSetCookie)
            {
                lock (locker)
                {
                    cookieStore.Save();
                }
            }

            return response;
        }

        /// <exception cref="System.IO.IOException"></exception>
        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null) {
                var mre = new ManualResetEvent(false);
                request.Content.LoadIntoBufferAsync().ContinueWith(t => mre.Set());
                mre.WaitOne(Manager.DefaultOptions.RequestTimeout, true);
            }

            return request;
        }

        #endregion

        #region Private

        private readonly HttpClientHandler context;
        private readonly CookieStore cookieStore;

        #endregion
    }
}

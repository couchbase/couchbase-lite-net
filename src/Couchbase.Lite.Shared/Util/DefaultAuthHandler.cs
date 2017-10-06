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
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;

using Couchbase.Lite.Auth;
using Couchbase.Lite.Util;

#if NET_3_5
using Cookie = System.Net.Couchbase.Cookie;
#endif

namespace Couchbase.Lite.Replicator
{

    internal sealed class DefaultAuthHandler : MessageProcessingHandler
    {

        #region Variables

        private object _locker = new object();
        private TimeSpan _timeout;
        private readonly CookieStore _cookieStore;
        private readonly ConcurrentDictionary<HttpResponseMessage, int> _retryMessages = new ConcurrentDictionary<HttpResponseMessage,int>();

        #endregion

        #region Properties

        internal IChallengeResponseAuthenticator Authenticator { get; set; }

        #endregion

        #region Constructors

        public DefaultAuthHandler(HttpClientHandler context, CookieStore cookieStore, TimeSpan timeout)
        {
            _cookieStore = cookieStore;
            _timeout = timeout;
            InnerHandler = context;
        }

        #endregion

        #region Overrides

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            int retryCount;
            do {
                if (Authenticator != null && response.StatusCode == HttpStatusCode.Unauthorized) {
                    retryCount = _retryMessages.GetOrAdd(response, 0);
                    if(retryCount >= 5) {
                        // Multiple concurrent requests means that the Nc can sometimes get out of order
                        // so try again, but within reason.
                        break;
                    }

                    _retryMessages.TryUpdate(response, retryCount + 1, retryCount);
                    var newRequest = new HttpRequestMessage(response.RequestMessage.Method, response.RequestMessage.RequestUri);
                    foreach (var header in response.RequestMessage.Headers) {
                        if(header.Key != "Authorization") {
                            newRequest.Headers.Add(header.Key, header.Value);
                        }
                    }

                    newRequest.Content = response.RequestMessage.Content;
                    var challengeResponse = Authenticator.ResponseFromChallenge(response);
                    if (challengeResponse != null) {
                        newRequest.Headers.Add("Authorization", challengeResponse);
                        return ProcessResponse(SendAsync(newRequest, cancellationToken).Result, cancellationToken);
                    }
                }
            }  while(false);

            var hasSetCookie = response.Headers.Contains("Set-Cookie");
            if (hasSetCookie) {
                var cookie = default(Cookie);
                if(CookieParser.TryParse(response.Headers.GetValues("Set-Cookie").ElementAt(0), response.RequestMessage.RequestUri.Host,
                    out cookie)) {
                    lock (_locker) {
                        try {
                            _cookieStore.Add(cookie);
                        } catch (CookieException e) {
                            var headerValue = new SecureLogString(response.Headers.GetValues("Set-Cookie").ElementAt(0),
                                LogMessageSensitivity.Insecure);
                            Log.To.Sync.W("DefaultAuthHandler",
                                $"Invalid cookie string received from remote: {headerValue}", e);
                        }
                    }
                }
            }

            _retryMessages.TryRemove(response, out retryCount);
            return response;
        }

        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if(request.Content != null && !(request.Content is CompressedContent)) {
                // This helps work around .NET 3.5's tendency to read from filestreams
                // multiple times (the second time will be zero length since the filestream
                // is already at the end)
                var mre = new ManualResetEvent(false);
                request.Content.LoadIntoBufferAsync().ConfigureAwait(false).GetAwaiter().OnCompleted(() => mre.Set());
                mre.WaitOne(_timeout, true);
            }

            return request;
        }

        #endregion

    }
}

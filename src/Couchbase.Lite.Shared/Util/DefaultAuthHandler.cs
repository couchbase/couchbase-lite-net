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
using System.Net.Http;
using System.Threading;

using Couchbase.Lite.Auth;
using Couchbase.Lite.Util;
using System.Net;
using System.Linq;

namespace Couchbase.Lite.Replicator
{

    internal sealed class DefaultAuthHandler : MessageProcessingHandler
    {

        #region Variables

        private bool _chunkedMode = false;
        private object _locker = new object();
        private readonly HttpClientHandler _context;
        private readonly CookieStore _cookieStore;

        #endregion


        #region Properties

        internal IChallengeResponseAuthenticator Authenticator { get; set; }

        #endregion

        #region Constructors

        public DefaultAuthHandler(HttpClientHandler context, CookieStore cookieStore, bool chunkedMode)
        {
            _chunkedMode = chunkedMode;
            _context = context;
            _cookieStore = cookieStore;
            InnerHandler = _context;
        }

        #endregion

        #region Overrides

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            /*if (response.Content != null && !_chunkedMode) {
                var mre = new ManualResetEvent(false);
                response.Content.LoadIntoBufferAsync().ConfigureAwait(false).GetAwaiter().OnCompleted(() => mre.Set());
                if (!mre.WaitOne(Manager.DefaultOptions.RequestTimeout, true)) {
                    Log.E("DefaultAuthHandler", "mre.WaitOne timed out: {0}", Environment.StackTrace);
                }
            }*/

            if (Authenticator != null && response.StatusCode == HttpStatusCode.Unauthorized 
                && !response.RequestMessage.Headers.Contains("Authorization")) {
                //Challenge received for the first time
                var newRequest = new HttpRequestMessage(response.RequestMessage.Method, response.RequestMessage.RequestUri);
                foreach (var header in response.RequestMessage.Headers) {
                    newRequest.Headers.Add(header.Key, header.Value);
                }

                newRequest.Content = response.RequestMessage.Content;
                var challengeResponse = Authenticator.ResponseFromChallenge(response);
                if (challengeResponse != null) {
                    newRequest.Headers.Add("Authorization", challengeResponse);
                    return ProcessResponse(SendAsync(newRequest, cancellationToken).Result, cancellationToken);
                }
            }

            var hasSetCookie = response.Headers.Contains("Set-Cookie");
            if (hasSetCookie) {
                lock (_locker) {
                    _cookieStore.Save();
                }
            }

            return response;
        }

        /// <exception cref="System.IO.IOException"></exception>
        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null) {
                var mre = new ManualResetEvent(false);
                request.Content.LoadIntoBufferAsync().ConfigureAwait(false).GetAwaiter().OnCompleted(() => mre.Set());
                mre.WaitOne(Manager.DefaultOptions.RequestTimeout, true);
            }

            return request;
        }

        #endregion

    }
}

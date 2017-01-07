//
// CouchbaseLiteHttpClient.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Util;
using Couchbase.Lite.Internal;

namespace Couchbase.Lite
{
    internal sealed class CouchbaseLiteHttpClient : IDisposable
    {
        private Leasable<HttpClient> _httpClient;
        private Leasable<DefaultAuthHandler> _authHandler;
        #if NET_3_5
        private int _connectionCount;
        private int _connectionLimit;
        #else
        private SemaphoreSlim _sendSemaphore;
        #endif


        public IAuthenticator Authenticator { get; set; }

        public TimeSpan Timeout
        {
            get {
                return _httpClient.Borrow(x => x.Timeout);
            }
            set {
                _httpClient.Borrow(x => x.Timeout = value);
            }
        }

        public CouchbaseLiteHttpClient(HttpClient client, DefaultAuthHandler authHandler)
        {
            _httpClient = client;
            _authHandler = authHandler;
            SetConcurrencyLimit(ReplicationOptions.DefaultMaxOpenHttpConnections);
        }

        public void SetConcurrencyLimit(int limit)
        {
            #if NET_3_5
            _connectionLimit = limit;
            #else
            Misc.SafeDispose(ref _sendSemaphore);
            _sendSemaphore = new SemaphoreSlim(limit, limit);
            #endif
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken token)
        {
            return SendAsync(message, HttpCompletionOption.ResponseContentRead, token);
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, HttpCompletionOption option, CancellationToken token)
        {
            #if NET_3_5
            if(_connectionCount >= _connectionLimit) {
                return Task.Delay(500).ContinueWith(t => SendAsync(message, option, token)).Unwrap();
            }

            Interlocked.Increment(ref _connectionCount);
            #else
            return _sendSemaphore?.WaitAsync()?.ContinueWith(t =>
            {
            #endif
                var challengeResponseAuth = Authenticator as IChallengeResponseAuthenticator;
                if (challengeResponseAuth != null) {
                    _authHandler.Borrow(x => x.Authenticator = challengeResponseAuth);
                    challengeResponseAuth.PrepareWithRequest(message);
                }

                var httpClient = default(HttpClient);
                if(!_httpClient.AcquireTemp(out httpClient)) {
                    return null;
                }


                (Authenticator as ICustomHeadersAuthorizer)?.AuthorizeRequest(message);
                return httpClient.SendAsync(message, option, token)
#if NET_3_5
                .ContinueWith(t =>
                {
                    Interlocked.Decrement(ref _connectionCount);
                    return t.Result;
                })
#endif
                ;
#if !NET_3_5
            })?.Unwrap()?.ContinueWith(t =>
            {
                message.Dispose();
                _sendSemaphore?.Release();
                if(t.IsFaulted) {
                    var e = t.Exception;
                    if(Misc.UnwrapAggregate(e) is ObjectDisposedException) {
                        return null;
                    }

                    throw e;
                }

                return t.Result;
            });
#endif
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _authHandler.Dispose();
#if !NET_3_5
            Misc.SafeDispose(ref _sendSemaphore);
#endif
        }
    }
}


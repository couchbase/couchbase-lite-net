//
// RetryStrategyExecutor.cs
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
using System.Threading;
using System.Net.Http;
using System.Threading.Tasks;

namespace Couchbase.Lite.Util
{
    internal sealed class RetryStrategyExecutor
    {
        private static readonly string Tag = typeof(RetryStrategyExecutor).Name;
        private readonly IRetryStrategy _strategy;
        private readonly CancellationToken _token;
        private HttpRequestMessage _request;

        public CancellationToken Token
        {
            get { return _token; }
        }

        public bool CanContinue
        {
            get { return _strategy.RetriesRemaining > 0; }
        }

        public Func<HttpRequestMessage, RetryStrategyExecutor, Task<HttpResponseMessage>> Send { get; set; }

        public RetryStrategyExecutor(HttpRequestMessage message, IRetryStrategy strategy, CancellationToken token)
        {
            _strategy = strategy;
            _request = message;
            _token = token;
        }

        public Task<HttpResponseMessage> Retry()
        {
            // If we send the same request again, then Mono (at least) will think it is already sent
            // and somehow get confused with the old one that is already finished sending.  This leads
            // to blocking until the request finally times out.  The same seems to apply if the content
            // is the same as well
            var initial = _request.Content == null ? Task.FromResult<byte[]>(null) : _request.Content.ReadAsByteArrayAsync();
            var newRequest = new HttpRequestMessage(_request.Method, _request.RequestUri);
            return initial.ContinueWith(t =>
            {
                if(t.Result != null) {
                    newRequest.Content = new ByteArrayContent(t.Result);
                    foreach (var header in _request.Content.Headers) {
                        newRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                foreach (var header in _request.Headers) {
                    newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                foreach (var property in _request.Properties) {
                    newRequest.Properties[property.Key] = property.Value;
                }

                newRequest.Version = _request.Version;
                _request.Dispose();
                _request = newRequest;

                Log.To.Sync.V(Tag, "{0} returned {1} for the next delay ({2} attempts remaining)",
                    _strategy.GetType().Name, _strategy.NextDelay(false), _strategy.RetriesRemaining - 1);

                return Task
                    .Delay(_strategy.NextDelay(true))
                    .ContinueWith(t1 => Send(newRequest, this))
                    .Unwrap();
            }).Unwrap();
        }
    }
}


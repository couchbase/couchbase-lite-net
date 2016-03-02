using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.IO;

namespace Couchbase.Lite.Util
{
    internal class ExponentialBackoffStrategy : IRetryStrategy
    {

        #region Variables

        private static readonly string Tag = typeof(ExponentialBackoffStrategy).Name;
        private readonly int _maxTries;

        private int _tries;
        private int _millis = 2000; // Wil be multiplied by 2 prior to use, so actually 4000
        private HttpRequestMessage _request;
        private readonly CancellationToken _token;

        #endregion

        #region Properties

        public int RetriesRemaining { get { return _maxTries - _tries; } }

        public Func<HttpRequestMessage, IRetryStrategy, Task<HttpResponseMessage>> Send { get; set; }

        public CancellationToken Token
        {
            get { return _token; }
        }

        #endregion

        #region Constructors

        public ExponentialBackoffStrategy(HttpRequestMessage request, int maxTries, CancellationToken token)
        {
            if (request == null) {
                Log.To.Database.E(Tag, "request is null in ctor, throwing...");
                throw new ArgumentNullException("request");
            }

            if (maxTries <= 0) {
                Log.To.Database.E(Tag, "maxTries <= 0 in ctor, throwing...");
                throw new ArgumentOutOfRangeException("maxTries", maxTries, "Max tries must be at least 1");
            }

            _token = token;
            _request = request;
            _maxTries = maxTries;
            _tries = 0;
        }

        #endregion

        #region IRetryStrategy implementation

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

                _tries++;
                _millis *= 2; // Double the wait backoff.
                return Task
                .Delay(_millis)
                .ContinueWith(t1 => Send(newRequest, this))
                .Unwrap();
            }).Unwrap();
        }

        #endregion

    }
}

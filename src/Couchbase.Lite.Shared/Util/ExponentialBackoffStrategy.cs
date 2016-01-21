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

        private readonly int _maxTries;

        private int _tries;
        private int _millis = 2000; // Wil be multiplied by 2 prior to use, so actually 4000
        private HttpRequestMessage _request;
        private byte[] _content;
        private ManualResetEvent _mre = new ManualResetEvent(false);
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
                throw new ArgumentNullException("request");
            }

            if (maxTries <= 0) {
                throw new ArgumentOutOfRangeException("maxTries", maxTries, "Max tries must be at least 1");
            }

            _token = token;
            if (request.Content != null) {
                request.Content.ReadAsByteArrayAsync().ContinueWith(t =>
                {
                    _content = t.Result;
                    _mre.Set();
                });
            } else {
                _mre.Set();
            }

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
            // to blocking until the request finally times out.
            var newRequest = new HttpRequestMessage(_request.Method, _request.RequestUri);
            if (_content != null) {
                _mre.WaitOne();
                newRequest.Content = new ByteArrayContent(_content);
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
                .ContinueWith(t => Send(newRequest, this))
                .Unwrap();
        }

        #endregion

    }
}

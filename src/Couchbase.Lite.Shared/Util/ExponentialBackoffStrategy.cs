using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace Couchbase.Lite.Util
{

    internal class ExponentialBackoffStrategy : IRetryStrategy
    {
        readonly HttpRequestMessage _request;
        readonly int _maxTries;

        int _tries;
        int _millis = 20;

        public ExponentialBackoffStrategy(HttpRequestMessage request, Int32 maxTries, CancellationToken token)
        {
            Token = token;

            _request = request;
            _maxTries = maxTries;
            _tries = 0;
        }

        public Int32 RetriesRemaining { get { return _maxTries - _tries; } }

        public SendMessageAsyncDelegate Send;

        #region IRetryStrategy implementation

        public CancellationToken Token { get; private set; }

        public Task<HttpResponseMessage> Retry()
        {
            _tries++;
            _millis *= 2; // Double the wait backoff.
            return Task
                .Delay(WaitInterval)
                .ContinueWith(t => Send(_request, Token))
                .Unwrap();
        }

        public TimeSpan WaitInterval
        {
            get {
                return TimeSpan.FromMilliseconds(_millis); 
            }
        }

        #endregion

    }
}

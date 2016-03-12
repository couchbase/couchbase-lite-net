using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace Couchbase.Lite.Util
{
    internal sealed class TransientErrorRetryHandler : DelegatingHandler
    {
        private static readonly string Tag = typeof(TransientErrorRetryHandler).Name;
        private readonly int _maxRetries;

        public TransientErrorRetryHandler(HttpMessageHandler handler, int maxRetries) : base(handler) 
        { 
            InnerHandler = handler;
            _maxRetries = maxRetries;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var strategy = new ExponentialBackoffStrategy(request, _maxRetries, cancellationToken);
            strategy.Send = ResendHandler;
            return ResendHandler(request, strategy);
        }

        private Task<HttpResponseMessage> ResendHandler(HttpRequestMessage request, IRetryStrategy strategy)
        {
            return base.SendAsync(request, strategy.Token)
                .ContinueWith(t => HandleTransientErrors(t, strategy), strategy.Token)
                .Unwrap();
        }

       
        static Task<HttpResponseMessage> HandleTransientErrors(Task<HttpResponseMessage> request, object state)
        {
            var strategy = (IRetryStrategy)state;
            if (!request.IsFaulted) 
            {
                var response = request.Result;
                if (strategy.RetriesRemaining > 0 && Misc.IsTransientError(response)) {
                    return strategy.Retry();
                }

                if (!response.IsSuccessStatusCode) {
                    Log.To.Sync.V(Tag, "Non transient error received ({0}), throwing HttpResponseException", 
                        response.StatusCode);
                    throw new HttpResponseException(response.StatusCode);
                }

                // If it's not faulted, there's nothing here to do.
                return request;
            }

            var error = Misc.Flatten(request.Exception);

            string statusCode;
            if (!Misc.IsTransientNetworkError(error, out statusCode) || strategy.RetriesRemaining == 0)
            {
                if (strategy.RetriesRemaining == 0) {
                    Log.To.Sync.V(Tag, "Out of retries for error, throwing", error);
                } else {
                    Log.To.Sync.V(Tag, "Non transient error received (status), throwing", error);
                }

                // If it's not transient, pass the exception along
                // for any other handlers to respond to.
                throw error;
            }

            // Retry again.
            return strategy.Retry();
        }
    }
}


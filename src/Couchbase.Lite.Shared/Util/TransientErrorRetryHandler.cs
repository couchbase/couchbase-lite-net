using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace Couchbase.Lite.Util
{
    internal sealed class TransientErrorRetryHandler : DelegatingHandler
    {
        public TransientErrorRetryHandler(HttpMessageHandler handler) : base(handler) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var strategy = new ExponentialBackoffStrategy(request, ManagerOptions.Default.MaxRetries, cancellationToken);
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
                    throw new HttpResponseException(response.StatusCode);
                }

                // If it's not faulted, there's nothing here to do.
                return request;
            }

            var error = request.Exception.Flatten().InnerException;

            if (!Misc.IsTransientNetworkError(error) || strategy.RetriesRemaining == 0)
            {
                // If it's not transient, pass the exception along
                // for any other handlers to respond to.
                throw error;
            }

            // Retry again.
            return strategy.Retry();
        }
    }
}


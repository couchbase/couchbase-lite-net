using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics.Contracts;

namespace Couchbase.Lite.Util
{
    internal sealed class TransientErrorRetryHandler : DelegatingHandler
    {
        public TransientErrorRetryHandler(HttpMessageHandler handler) : base(handler) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var strategy = new ExponentialBackoffStrategy(request, ManagerOptions.Default.MaxRetries, cancellationToken);
            strategy.Send += base.SendAsync;

            return base.SendAsync(request, cancellationToken)
                .ContinueWith(t => HandleTransientErrors(t, strategy), cancellationToken)
                .Unwrap();
        }

       
        static Task<HttpResponseMessage> HandleTransientErrors(Task<HttpResponseMessage> request, object state)
        {
            var strategy = (IRetryStrategy)state;
            if (!request.IsFaulted) 
            {
                // If it's not faulted, there's nothing here to do.
                return request;
            }

            var error = request.Exception.Flatten().InnerException;

            if (!Misc.IsTransientNetworkError(error) || (request.Exception.Flatten().InnerException is HttpRequestException && Misc.IsTransientError(request.Result) && strategy.RetriesRemaining == 0))
            {
                // If it's not transient, pass the exception along
                // for any other handlers to respond to.
                throw error;
            }

            // Retry again.
            return strategy
                .Retry()
                .ContinueWith(t => HandleTransientErrors(t, strategy), strategy.Token)
                .Unwrap();
        }
    }
}


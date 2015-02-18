using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace Couchbase.Lite.Support
{
    public class CouchbaseLiteHttpClient : HttpClient
    {
        public CouchbaseLiteHttpClient(HttpMessageHandler handler, bool disposeHandler) : base(handler, disposeHandler) {}

        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Task<HttpResponseMessage> retVal = null;
            try {
                retVal = base.SendAsync(request, cancellationToken);
            } catch(Exception e) {
                //.NET 4.0.x logic to bring it inline with 4.5
                TaskCompletionSource<HttpResponseMessage> completionSource = new TaskCompletionSource<HttpResponseMessage>();
                if (e is OperationCanceledException) {
                    completionSource.SetCanceled();
                } else {
                    completionSource.SetException(e);
                }
                    
                retVal = completionSource.Task;
            }

            return retVal;
        }
    }
}


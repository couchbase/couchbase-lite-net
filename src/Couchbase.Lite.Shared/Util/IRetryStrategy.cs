using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace Couchbase.Lite.Util
{
    internal delegate Task<HttpResponseMessage> SendMessageAsyncDelegate(HttpRequestMessage request, CancellationToken cancellationToken);

	interface IRetryStrategy
	{
        Task<HttpResponseMessage> Retry();
        int RetriesRemaining { get; }
        CancellationToken Token { get; }
	}
}


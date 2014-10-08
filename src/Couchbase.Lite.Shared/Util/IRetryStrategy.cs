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
        Int32 RetriesRemaining { get; }
        CancellationToken Token { get; }
	}
}


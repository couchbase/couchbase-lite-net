using System;
using System.Net.Http;
using Couchbase.Lite.Support;
using Couchbase.Lite.Replicator;

namespace Couchbase.Lite.Tests
{
    public class AlwaysFailingClientFactory : IHttpClientFactory
    {
        public HttpClientHandler HttpHandler { get ; set; }

        public AlwaysFailingClientFactory()
        {
            HttpHandler = new FailEveryRequestHandler();
        }

        public HttpClient GetHttpClient()
        {
            var mockHttpClient = new HttpClient(HttpHandler);
            return mockHttpClient;
        }
    }
}


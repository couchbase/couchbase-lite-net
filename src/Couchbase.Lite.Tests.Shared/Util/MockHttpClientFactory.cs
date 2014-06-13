using System;
using System.Net.Http;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using System.Collections.Generic;

namespace Couchbase.Lite.Tests
{
    public class MockHttpClientFactory : IHttpClientFactory
    {
        const string Tag = "MockHttpClientFactory";

        public HttpClientHandler HttpHandler { get; private set;}

        public System.Collections.Generic.IDictionary<string, string> Headers { get; set; }

        public MockHttpClientFactory()
        {
            HttpHandler = new MockHttpRequestHandler();
            Headers = new Dictionary<string,string>();
        }

        public HttpClient GetHttpClient()
        {
            var client = new HttpClient(HttpHandler);

            foreach(var header in Headers)
            {
                var success = client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                if (!success)
                {
                    Log.W(Tag, "Unabled to add header to request: {0}: {1}".Fmt(header.Key, header.Value));
                }
            }
            return client;
        }
    }
}
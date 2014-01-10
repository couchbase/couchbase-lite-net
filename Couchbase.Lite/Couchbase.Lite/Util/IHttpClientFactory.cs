using System;
using System.Net.Http;

namespace Couchbase.Lite.Support
{
    public interface IHttpClientFactory
    {
        HttpClient GetHttpClient();
    }
}


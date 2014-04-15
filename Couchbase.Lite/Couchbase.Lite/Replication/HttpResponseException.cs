using System;
using System.Net;

namespace Couchbase.Lite
{
    public class HttpResponseException : Exception
    {
        internal HttpStatusCode StatusCode { get; set; }

        public HttpResponseException (HttpStatusCode statusCode) { StatusCode = statusCode; }
    }
}


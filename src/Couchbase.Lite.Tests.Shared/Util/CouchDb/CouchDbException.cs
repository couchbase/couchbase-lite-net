using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Tests.Shared.Util.CouchDb
{
    public class CouchDbException : Exception
    {
        public int StatusCode { get; protected set; }
        public CouchDbException(int statusCode) : base()
        {
            StatusCode = statusCode;
        }

        public CouchDbException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }

        public CouchDbException(string message, Exception innerException, int statusCode) : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}

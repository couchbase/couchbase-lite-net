using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Tests.Shared.Util.CouchBaseLite
{
    public class ReplicationHelperException : Exception
    {
        public ReplicationHelperException() : base()
        {

        }

        public ReplicationHelperException(string message) : base(message)
        {

        }

        public ReplicationHelperException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}

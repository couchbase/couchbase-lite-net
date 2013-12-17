using System;

namespace Couchbase.Lite {

    public class CouchbaseLiteException : ApplicationException {

        public CouchbaseLiteException (string message) : base(message) {  }

        public CouchbaseLiteException (string messageFormat, params Object[] values)
            : base(String.Format(messageFormat, values)) {  }

    }

}
using System;

namespace Couchbase.Lite {

    public class CouchbaseLiteException : ApplicationException {

        public CouchbaseLiteException (Exception innerException, StatusCode code) : base(String.Format("Database error: {0}", code), innerException) { }

        public CouchbaseLiteException (Exception innerException, Status code) : this(innerException, code.GetCode()) { }

        public CouchbaseLiteException (StatusCode code) : base(String.Format("Database error: {0}", code)) { }

        public CouchbaseLiteException (string message) : base(message) {  }

        public CouchbaseLiteException (string messageFormat, params Object[] values)
            : base(String.Format(messageFormat, values)) {  }

        public Status GetCBLStatus ()
        {
            throw new NotImplementedException ();
        }
    }

}
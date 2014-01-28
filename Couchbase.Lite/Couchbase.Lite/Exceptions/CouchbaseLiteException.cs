using System;

namespace Couchbase.Lite {

    public class CouchbaseLiteException : ApplicationException {

        internal StatusCode Code { get; set; }

        public CouchbaseLiteException (Exception innerException, StatusCode code) : base(String.Format("Database error: {0}", code), innerException) { Code = code; }

        public CouchbaseLiteException (Exception innerException, Status status) : this(innerException, status.GetCode()) { Code = status.GetCode(); }

        public CouchbaseLiteException (StatusCode code) : base(String.Format("Database error: {0}", code)) { Code = code; }

        public CouchbaseLiteException (String message, StatusCode code) : base(message) { Code = code; }

        public CouchbaseLiteException (String message) : base(message) {  }

        public CouchbaseLiteException (String messageFormat, params Object[] values)
            : base(String.Format(messageFormat, values)) {  }

        public Status GetCBLStatus ()
        {
            return new Status(Code);
        }
    }

}
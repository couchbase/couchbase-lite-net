using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    

    public partial class QueryRow {

    #region Instance Members
        //Properties
        public Database Database { get { throw new NotImplementedException(); } }

        public Document Document { get { throw new NotImplementedException(); } }

        public Object Key { get { throw new NotImplementedException(); } }

        public Object Value { get { throw new NotImplementedException(); } }

        public String DocumentId { get { throw new NotImplementedException(); } }

        public String SourceDocumentID { get { throw new NotImplementedException(); } }

        public String DocumentRevisionId { get { throw new NotImplementedException(); } }

        public Dictionary<String, Object> DocumentProperties { get { throw new NotImplementedException(); } }

        public long SequenceNumber { get { throw new NotImplementedException(); } }

    #endregion
    
    }

}

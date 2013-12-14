using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    public partial class Attachment {

    #region Instance Members
        //Properties
        public Revision Revision { get { throw new NotImplementedException(); } }

        public Document Document { get { throw new NotImplementedException(); } }

        public String Name { get { throw new NotImplementedException(); } }

        public String ContentType { get { throw new NotImplementedException(); } }

        public IEnumerable<byte> Content { get { throw new NotImplementedException(); } }

        public Int64 Length { get { throw new NotImplementedException(); } }

        public Dictionary<String, Object> Metadata { get { throw new NotImplementedException(); } }

    #endregion
    
    }

}


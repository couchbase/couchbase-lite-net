using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    

    public partial class QueryEnumerator {

    #region Instance Members
        //Properties
        public int Count { get { throw new NotImplementedException(); } }

        public long SequenceNumber { get { throw new NotImplementedException(); } }

        public Boolean Stale { get { throw new NotImplementedException(); } }

        //Methods
        public QueryRow Next() { throw new NotImplementedException(); }

        public QueryRow GetRow(int index) { throw new NotImplementedException(); }

        public void Reset() { throw new NotImplementedException(); }

    #endregion
    
    }

    

}

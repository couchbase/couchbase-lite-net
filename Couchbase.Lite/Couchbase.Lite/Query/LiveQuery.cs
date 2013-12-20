using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    

    public partial class LiveQuery : Query {

    #region Constructors

        internal LiveQuery(Query query) : base(query.Database, query.View)
        {
            throw new NotImplementedException();
//            SetLimit(query.GetLimit());
//            SetSkip(query.GetSkip());
//            SetStartKey(query.GetStartKey());
//            SetEndKey(query.GetEndKey());
//            SetDescending(query.IsDescending());
//            SetPrefetch(query.ShouldPrefetch());
//            SetKeys(query.GetKeys());
//            SetGroupLevel(query.GetGroupLevel());
//            SetMapOnly(query.IsMapOnly());
//            SetStartKeyDocId(query.GetStartKeyDocId());
//            SetEndKeyDocId(query.GetEndKeyDocId());
//            SetIndexUpdateMode(query.GetIndexUpdateMode());
        }
    
    #endregion

    #region Instance Members
        //Properties
        public QueryEnumerator Rows { get { throw new NotImplementedException(); } }

        public Exception LastError { get { throw new NotImplementedException(); } }

        //Methods
        public void Start() { throw new NotImplementedException(); }

        public void Stop() { throw new NotImplementedException(); }

        public void WaitForRows() { throw new NotImplementedException(); }

        public event EventHandler<QueryChangeEventArgs> Change;

    #endregion
    
    #region Delegates

    #endregion
    
    #region EventArgs Subclasses
        public class QueryChangeEventArgs : EventArgs {

            //Properties
            public LiveQuery Source { get { throw new NotImplementedException(); } }

            public QueryEnumerator Rows { get { throw new NotImplementedException(); } }

            public Exception Error { get { throw new NotImplementedException(); } }

        }

    #endregion
    
    }

    

    

}

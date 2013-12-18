using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    public partial class Query {

    #region Enums
    
    public enum IndexUpdateMode {
        Before,
        Never,
        After
    }
                        
    #endregion
    
    #region Instance Members
        //Properties
        public Database Database { get { throw new NotImplementedException(); } }

        public int Limit { get; set; }

        public int Skip { get; set; }

        public Boolean Descending { get; set; }

        public Object StartKey { get; set; }

        public Object EndKey { get; set; }

        public String StartKeyDocId { get; set; }

        public String EndKeyDocId { get; set; }

        public IndexUpdateMode UpdateMode { get; set; }

        public IEnumerable<Object> Keys { get; set; }

        public Boolean MapOnly { get; set; }

        public int GroupLevel { get; set; }

        public Boolean Prefetch { get; set; }

        public Boolean IncludeDeleted { get; set; }

        //Methods
        public QueryEnumerator Run() { throw new NotImplementedException(); }

        public void RunAsync(QueryCompleteDelegate onComplete) { throw new NotImplementedException(); }

        public LiveQuery ToLiveQuery() { throw new NotImplementedException(); }

    #endregion
    
    #region Delegates
        public delegate void QueryCompleteDelegate(QueryEnumerator rows, Exception error);

    #endregion
    
    }

    public partial class LiveQuery : Query {

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


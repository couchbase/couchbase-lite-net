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


    #region Constructors
        internal Query(Database database, View view)
        {
            throw new NotImplementedException();
//            // null for _all_docs query
//            Database = database;
//            View = view;
//            limit = int.MaxValue;
//            mapOnly = (view != null && view.GetReduce() == null);
//            indexUpdateMode = Query.IndexUpdateMode.Never;
        }

        /// <summary>Constructor</summary>
        internal Query(Database database, MapDelegate mapFunction) : this(database, database.MakeAnonymousView())
        {
            throw new NotImplementedException();
//            temporaryView = true;
//            view.SetMap(mapFunction, string.Empty);
        }

        /// <summary>Constructor</summary>
        internal Query(Database database, Couchbase.Lite.Query query) : this(database, query.View)
        {
            throw new NotImplementedException();
//            limit = query.limit;
//            skip = query.skip;
//            startKey = query.startKey;
//            endKey = query.endKey;
//            descending = query.descending;
//            prefetch = query.prefetch;
//            keys = query.keys;
//            groupLevel = query.groupLevel;
//            mapOnly = query.mapOnly;
//            startKeyDocId = query.startKeyDocId;
//            endKeyDocId = query.endKeyDocId;
//            indexUpdateMode = query.indexUpdateMode;
        }


    #endregion
       
    #region Non-public Members

        internal View View { get; private set; }

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
}


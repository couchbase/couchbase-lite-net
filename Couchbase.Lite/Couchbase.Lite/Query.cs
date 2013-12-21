using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;
using System.Threading.Tasks;

namespace Couchbase.Lite {

    public enum IndexUpdateMode {
            Before,
            Never,
            After
    }
                            
        public partial class Query : IDisposable
    {

    #region Constructors
        internal Query(Database database, View view)
        {
            // null for _all_docs query
            Database = database;
            View = view;
            Limit = Int32.MaxValue;
            MapOnly = (view != null && view.Reduce == null);
            IndexUpdateMode = IndexUpdateMode.Never;
        }

        /// <summary>Constructor</summary>
        internal Query(Database database, MapDelegate mapFunction)
        : this(database, database.MakeAnonymousView())
        {
            TemporaryView = true;
            View.SetMap(mapFunction, string.Empty);
        }

        /// <summary>Constructor</summary>
        internal Query(Database database, Query query) 
        : this(database, query.View)
        {
            Limit = query.Limit;
            Skip = query.Skip;
            StartKey = query.StartKey;
            EndKey = query.EndKey;
            Descending = query.Descending;
            Prefetch = query.Prefetch;
            Keys = query.Keys;
            GroupLevel = query.GroupLevel;
            MapOnly = query.MapOnly;
            StartKeyDocId = query.StartKeyDocId;
            EndKeyDocId = query.EndKeyDocId;
            IndexUpdateMode = query.IndexUpdateMode;
        }


    #endregion
       
    #region Non-public Members

        internal View View { get; private set; }

        private  bool TemporaryView { get; set; }

        private Int64 LastSequence { get; set; }

        private QueryOptions QueryOptions
        {
            get {
                var queryOptions = new QueryOptions();
                queryOptions.SetStartKey(StartKey);
                queryOptions.SetEndKey(EndKey);
                queryOptions.SetStartKey(StartKey);
                queryOptions.SetKeys(Keys);
                queryOptions.SetSkip(Skip);
                queryOptions.SetLimit(Limit);
                queryOptions.SetReduce(!MapOnly);
                queryOptions.SetReduceSpecified(true);
                queryOptions.SetGroupLevel(GroupLevel);
                queryOptions.SetDescending(Descending);
                queryOptions.SetIncludeDocs(Prefetch);
                queryOptions.SetUpdateSeq(true);
                queryOptions.SetInclusiveEnd(true);
                queryOptions.SetIncludeDeletedDocs(IncludeDeleted);
                queryOptions.SetStale(IndexUpdateMode);
                return queryOptions;
            }
        }


    #endregion

    #region Instance Members
        //Properties
        public Database Database { get; private set; }

        public Int32 Limit { get; set; }

        public Int32 Skip { get; set; }

        public Boolean Descending { get; set; }

        public Object StartKey { get; set; }

        public Object EndKey { get; set; }

        public String StartKeyDocId { get; set; }

        public String EndKeyDocId { get; set; }

        public IndexUpdateMode IndexUpdateMode { get; set; }

        public IEnumerable<Object> Keys { get; set; }

        public Boolean MapOnly { get; set; }

        public Int32 GroupLevel { get; set; }

        public Boolean Prefetch { get; set; }

        public Boolean IncludeDeleted { get; set; }

        public event EventHandler<QueryCompletedEventArgs> Completed;

        //Methods
        public QueryEnumerator Run() 
        {
            var outSequence = new AList<long>();
            var viewName = (View != null) ? View.Name : null;

            IEnumerable<QueryRow> rows = null;
            Exception error = null;
            try {
                rows = Database.QueryViewNamed (viewName, QueryOptions, outSequence);
            } catch (Exception ex) {
                error = ex;
            }

            LastSequence = outSequence[0];

            var completed = Completed;
            if (completed != null)
            {
                var enumerator = new QueryEnumerator(Database, rows, LastSequence);
                var args = new QueryCompletedEventArgs(enumerator, error);
                completed(this, args);
            }
            return new QueryEnumerator(Database, rows, LastSequence);
        }

        public Task<QueryEnumerator> RunAsync() {
            return Task.Factory.StartNew<QueryEnumerator>(Run);
        }

        public LiveQuery ToLiveQuery() 
        {
            if (View == null)
            {
                throw new CouchbaseLiteException("Cannot convert a Query to LiveQuery if the view is null");
            }
            return new LiveQuery(this);
        }

        public void Dispose()
        {
            if (TemporaryView)
                View.Delete();
        }
    #endregion
    
    #region Delegates

        public delegate void QueryCompleteDelegate(QueryEnumerator rows, Exception error);

    #endregion
    
    }
}


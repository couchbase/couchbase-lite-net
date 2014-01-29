using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;
using System.Threading.Tasks;
using System.Threading;

namespace Couchbase.Lite {

    public enum IndexUpdateMode {
            Before,
            Never,
            After
    }
                            
    public enum AllDocsMode
    {
        AllDocs,
        IncludeDeleted,
        ShowConflicts,
        OnlyConflicts
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
            AllDocsMode = AllDocsMode.AllDocs;
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
            AllDocsMode = query.AllDocsMode;
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
                queryOptions.SetAllDocsMode(AllDocsMode);
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

        /// <summary>Changes the behavior of a query created by -queryAllDocuments.</summary>
        /// <remarks>
        /// Changes the behavior of a query created by -queryAllDocuments.
        /// - In mode kCBLAllDocs (the default), the query simply returns all non-deleted documents.
        /// - In mode kCBLIncludeDeleted, it also returns deleted documents.
        /// - In mode kCBLShowConflicts, the .conflictingRevisions property of each row will return the
        /// conflicting revisions, if any, of that document.
        /// - In mode kCBLOnlyConflicts, _only_ documents in conflict will be returned.
        /// (This mode is especially useful for use with a CBLLiveQuery, so you can be notified of
        /// conflicts as they happen, i.e. when they're pulled in by a replication.)
        /// </remarks>
        public AllDocsMode AllDocsMode { get; set; }

        public IEnumerable<Object> Keys { get; set; }

        public Boolean MapOnly { get; set; }

        public Int32 GroupLevel { get; set; }

        public Boolean Prefetch { get; set; }

        public Boolean IncludeDeleted 
        {
            get { return AllDocsMode == AllDocsMode.IncludeDeleted; }
            set 
            {
                   AllDocsMode = (value)
                                 ? AllDocsMode.IncludeDeleted
                                 : AllDocsMode.AllDocs;
            } 
        }

        public event EventHandler<QueryCompletedEventArgs> Completed;

        //Methods
        public virtual QueryEnumerator Run() 
        {
            var outSequence = new AList<long>();
            var viewName = (View != null) ? View.Name : null;
            var queryOptions = QueryOptions;

            var rows = Database.QueryViewNamed (viewName, queryOptions, outSequence);

            LastSequence = outSequence[0]; // potential concurrency issue?

            return new QueryEnumerator(Database, rows, outSequence[0]);
        }

        public Task<QueryEnumerator> RunAsync(Func<QueryEnumerator> run, CancellationToken token) 
        {
            return Database.Manager.RunAsync(run, token)
                    .ContinueWith(runTask=> // Raise the query's Completed event.
                        {
                            var error = runTask.Exception;

                            var completed = Completed;
                            if (completed != null)
                            {
                                var args = new QueryCompletedEventArgs(runTask.Result, error);
                                completed(this, args);
                            }

                            if (runTask.Status != TaskStatus.RanToCompletion)
                                throw runTask.Exception; // Rethrow innner exceptions.

                            return runTask.Result; // Give additional continuation functions access to the results task.
                    });
        }

        public Task<QueryEnumerator> RunAsync() 
        {
            return RunAsync(Run, CancellationToken.None);
        }

//        internal Task<QueryEnumerator> RunAsync(Func<QueryEnumerator> action) 
//        {
//            return Database.Manager.RunAsync(action, CancellationToken.None)
//                .ContinueWith(runTask=> // Raise the query's Completed event.
//                    {
//                        var error = runTask.Exception;
//
//                        var completed = Completed;
//                        if (completed != null)
//                        {
//                            var args = new QueryCompletedEventArgs(runTask.Result, error);
//                            completed(this, args);
//                        }
//
//                        if (runTask.Status != TaskStatus.RanToCompletion)
//                            throw runTask.Exception; // Rethrow innner exceptions.
//
//                        return runTask.Result; // Give additional continuation functions access to the results task.
//                    });
//        }

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
    }

    #region Delegates

    public delegate void QueryCompleteDelegate(QueryEnumerator rows, Exception error);

    #endregion
        
}


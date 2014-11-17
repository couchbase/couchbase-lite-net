using System;
using Couchbase.Lite.Portable;

namespace Couchbase.Lite.Portable
{
    public interface IQuery : IDatabaseHolder
    {
        AllDocsMode AllDocsMode { get; set; }
        event EventHandler<QueryCompletedEventArgs> Completed;
        bool Descending { get; set; }
        void Dispose();
        object EndKey { get; set; }
        string EndKeyDocId { get; set; }
        int GroupLevel { get; set; }
        bool IncludeDeleted { get; set; }
        bool InclusiveEnd { get; set; }
        IndexUpdateMode IndexUpdateMode { get; set; }
        System.Collections.Generic.IEnumerable<object> Keys { get; set; }
        int Limit { get; set; }
        bool MapOnly { get; set; }
        bool Prefetch { get; set; }
        Couchbase.Lite.Portable.IQueryEnumerator Run();
        System.Threading.Tasks.Task<Couchbase.Lite.Portable.IQueryEnumerator> RunAsync();
        System.Threading.Tasks.Task<Couchbase.Lite.Portable.IQueryEnumerator> RunAsync(Func<Couchbase.Lite.Portable.IQueryEnumerator> run, System.Threading.CancellationToken token);
        int Skip { get; set; }
        object StartKey { get; set; }
        string StartKeyDocId { get; set; }
        Couchbase.Lite.Portable.ILiveQuery ToLiveQuery();
    }
}

namespace Couchbase.Lite
{
    /// <summary>
    /// Query completed event arguments.
    /// </summary>
    public class QueryCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// The result rows from the Query.
        /// </summary>
        /// <value>he result rows.</value>
        public IQueryEnumerator Rows { get; private set; }

        /// <summary>
        /// The error, if any, that occured during the execution of the Query
        /// </summary>
        /// <value>The error info if any.</value>
        public Exception ErrorInfo { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Couchbase.Lite.QueryCompletedEventArgs"/> class.
        /// </summary>
        /// <param name="rows">Rows.</param>
        /// <param name="errorInfo">Error info.</param>
        public QueryCompletedEventArgs(IQueryEnumerator rows, Exception errorInfo)
        {
            Rows = rows;
            ErrorInfo = errorInfo;
        }
    }
}
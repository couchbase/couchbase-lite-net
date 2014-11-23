using System;
using Couchbase.Lite.Portable;

namespace Couchbase.Lite.Portable
{
    public interface ILiveQuery:IQuery
    {
        event EventHandler<global::Couchbase.Lite.QueryChangeEventArgs> Changed;
        Exception LastError { get; }
        global::Couchbase.Lite.Portable.IQueryEnumerator Rows { get; }
        global::Couchbase.Lite.Portable.IQueryEnumerator Run();
        void Start();
        void Stop();
        void WaitForRows();
    }

    
}

namespace Couchbase.Lite
{
    #region EventArgs Subclasses

    /// <summary>
    /// Query change event arguments.
    /// </summary>
    public class QueryChangeEventArgs : EventArgs
    {
        public QueryChangeEventArgs(ILiveQuery liveQuery, IQueryEnumerator enumerator, Exception error)
        {
            Source = liveQuery;
            Rows = enumerator;
            Error = error;
        }

        //Properties
        /// <summary>
        /// Gets the LiveQuery that raised the event.
        /// </summary>
        /// <value>The LiveQuery that raised the event.</value>
        public ILiveQuery Source { get; private set; }

        /// <summary>
        /// Gets the results of the Query.
        /// </summary>
        /// <value>The results of the Query.</value>
        public IQueryEnumerator Rows { get; private set; }

        /// <summary>
        /// Returns the error, if any, that occured while executing 
        /// the <see cref="Couchbase.Lite.Query"/>, otherwise null.
        /// </summary>
        /// <value>The error.</value>
        public Exception Error { get; private set; }
    }

    #endregion
}

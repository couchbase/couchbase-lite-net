using System;
namespace Couchbase.Lite.Portable
{
    public interface IDatabase
    {
        System.Collections.Generic.IEnumerable<Couchbase.Lite.Portable.IReplication> AllReplications { get; }
        event EventHandler<DatabaseChangeEventArgs> Changed;
        void Compact();
        Couchbase.Lite.Portable.IQuery CreateAllDocumentsQuery();
        Couchbase.Lite.Portable.IDocument CreateDocument();
        Couchbase.Lite.Portable.IReplication CreatePullReplication(Uri url);
        Couchbase.Lite.Portable.IReplication CreatePushReplication(Uri url);
        void Delete();
        bool DeleteLocalDocument(string id);
        int DocumentCount { get; }
        Couchbase.Lite.Portable.IDocument GetDocument(string id);
        Couchbase.Lite.Portable.IDocument GetExistingDocument(string id);
        System.Collections.Generic.IDictionary<string, object> GetExistingLocalDocument(string id);
        Couchbase.Lite.Portable.IView GetExistingView(string name);
        Couchbase.Lite.Portable.FilterDelegate GetFilter(string name);
        Couchbase.Lite.Portable.ValidateDelegate GetValidation(string name);
        Couchbase.Lite.Portable.IView GetView(string name);
        long LastSequenceNumber { get; }
        Couchbase.Lite.Portable.IManager Manager { get; }
        string Name { get; }
        System.Net.CookieContainer PersistentCookieStore { get; }
        void PutLocalDocument(string id, System.Collections.Generic.IDictionary<string, object> properties);
        System.Threading.Tasks.Task RunAsync(RunAsyncDelegate runAsyncDelegate);
        bool RunInTransaction(RunInTransactionDelegate transactionDelegate);
        bool SequenceHasAttachments(long sequence);
        void SetFilter(string name, FilterDelegate filterDelegate);
        void SetValidation(string name, ValidateDelegate validationDelegate);
        string ToString();
    }
}
using System;
namespace Couchbase.Lite.Portable
{
    public interface IQueryRow
    {
        global::System.Collections.Generic.IDictionary<string, object> AsJSONDictionary();
        global::Couchbase.Lite.Portable.IDatabase Database { get; }
        global::Couchbase.Lite.Portable.IDocument Document { get; }
        string DocumentId { get; }
        global::System.Collections.Generic.IDictionary<string, object> DocumentProperties { get; }
        string DocumentRevisionId { get; }
        bool Equals(object obj);
        global::System.Collections.Generic.IEnumerable<global::Couchbase.Lite.Portable.ISavedRevision> GetConflictingRevisions();
        object Key { get; }
        long SequenceNumber { get; }
        string SourceDocumentId { get; }
        string ToString();
        object Value { get; }
    }
}

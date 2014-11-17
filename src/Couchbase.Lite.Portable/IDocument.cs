using System;

namespace Couchbase.Lite.Portable
{
    /// <summary>
    /// Standard Document definition for Couchbase.
    /// </summary>
    public interface IDocument
    {
        event EventHandler<DocumentChangeEventArgs> Change;
        System.Collections.Generic.IEnumerable<ISavedRevision> ConflictingRevisions { get; }
        IUnsavedRevision CreateRevision();
        ISavedRevision CurrentRevision { get; }
        string CurrentRevisionId { get; }
        Couchbase.Lite.Portable.IDatabase Database { get; }
        void Delete();
        bool Deleted { get; }
        object GetProperty(string key);
        TValue GetProperty<TValue>(string key);
        Couchbase.Lite.Portable.ISavedRevision GetRevision(string id);
        string Id { get; set; }
        System.Collections.Generic.IEnumerable<ISavedRevision> LeafRevisions { get; }
        System.Collections.Generic.IDictionary<string, object> Properties { get; }
        void Purge();
        ISavedRevision PutProperties(System.Collections.Generic.IDictionary<string, object> properties);
        System.Collections.Generic.IEnumerable<ISavedRevision> RevisionHistory { get; }
        ISavedRevision Update(DocUpdateDelegate updateDelegate);
        System.Collections.Generic.IDictionary<string, object> UserProperties { get; }
    }
}
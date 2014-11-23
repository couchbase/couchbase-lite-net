using System;

namespace Couchbase.Lite.Portable
{
    public interface IRevision
    {
        System.Collections.Generic.IEnumerable<string> AttachmentNames { get; }
        System.Collections.Generic.IEnumerable<Couchbase.Lite.Portable.IAttachment> Attachments { get; }
        Couchbase.Lite.Portable.IDatabase Database { get; }
        Couchbase.Lite.Portable.IDocument Document { get; }
        bool Equals(object obj);
        Couchbase.Lite.Portable.IAttachment GetAttachment(string name);
        int GetHashCode();
        object GetProperty(string key);
        string Id { get; }
        bool IsDeletion { get; }
        bool IsGone { get; }
        Couchbase.Lite.Portable.ISavedRevision Parent { get; }
        string ParentId { get; }
        System.Collections.Generic.IDictionary<string, object> Properties { get; }
        System.Collections.Generic.IEnumerable<ISavedRevision> RevisionHistory { get; }
        string ToString();
        System.Collections.Generic.IDictionary<string, object> UserProperties { get; }
    }
}

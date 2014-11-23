using System;
namespace Couchbase.Lite.Portable
{
    public interface ISavedRevision
    {
        IUnsavedRevision CreateRevision();
        ISavedRevision CreateRevision(global::System.Collections.Generic.IDictionary<string, object> properties);
        ISavedRevision DeleteDocument();
        string Id { get; }
        bool IsDeletion { get; }
        ISavedRevision Parent { get; }
        string ParentId { get; }
        string ParentRevisionID { get; set; }
        global::System.Collections.Generic.IDictionary<string, object> Properties { get; }
        bool PropertiesAvailable { get; }
        global::System.Collections.Generic.IEnumerable<ISavedRevision> RevisionHistory { get; }
    }
}

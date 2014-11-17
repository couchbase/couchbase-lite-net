using System;
namespace Couchbase.Lite.Portable
{
    public interface IUnsavedRevision:IRevision
    {
        string Id { get; }
        //bool IsDeletion { get; set; }
        //Couchbase.Lite.Portable.ISavedRevision Parent { get; }
        //string ParentId { get; }
        //System.Collections.Generic.IDictionary<string, object> Properties { get; }
        void RemoveAttachment(string name);
        //System.Collections.Generic.IEnumerable<Couchbase.Lite.SavedRevision> RevisionHistory { get; }
        ISavedRevision Save();
        ISavedRevision Save(bool allowConflict);
        void SetAttachment(string name, string contentType, System.Collections.Generic.IEnumerable<byte> content);
        void SetAttachment(string name, string contentType, System.IO.Stream content);
        void SetAttachment(string name, string contentType, Uri contentUrl);
        void SetProperties(System.Collections.Generic.IDictionary<string, object> newProperties);
        void SetUserProperties(System.Collections.Generic.IDictionary<string, object> userProperties);
    }
}

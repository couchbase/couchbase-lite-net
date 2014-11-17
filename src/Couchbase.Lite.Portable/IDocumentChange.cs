using System;
namespace Couchbase.Lite.Portable
{
    public interface IDocumentChange
    {
        string DocumentId { get; }
        bool IsConflict { get; }
        bool IsCurrentRevision { get; }
        string RevisionId { get; }
        Uri SourceUrl { get; }
    }
}

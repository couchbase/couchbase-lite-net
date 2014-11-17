using System;

namespace Couchbase.Lite.Portable
{
    public interface IAttachment:IDisposable
    {
        System.Collections.Generic.IEnumerable<byte> Content { get; }
        System.IO.Stream ContentStream { get; }
        string ContentType { get; }
        Couchbase.Lite.Portable.IDocument Document { get; }
        long Length { get; }
        System.Collections.Generic.IDictionary<string, object> Metadata { get; }
        string Name { get; }
        Couchbase.Lite.Portable.IRevision Revision { get; }
    }
}

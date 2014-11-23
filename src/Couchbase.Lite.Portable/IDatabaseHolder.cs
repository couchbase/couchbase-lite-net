using System;
namespace Couchbase.Lite.Portable
{
    public interface IDatabaseHolder
    {
        global::Couchbase.Lite.Portable.IDatabase Database { get; }
    }
}

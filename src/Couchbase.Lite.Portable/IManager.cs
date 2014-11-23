using System;
namespace Couchbase.Lite.Portable
{
    public interface IManager
    {
        System.Collections.Generic.IEnumerable<string> AllDatabaseNames { get; }
        void Close();
        string Directory { get; }
        void ForgetDatabase(Couchbase.Lite.Portable.IDatabase database);
        Couchbase.Lite.Portable.IDatabase GetDatabase(string name);
        Couchbase.Lite.Portable.IDatabase GetExistingDatabase(string name);
        void ReplaceDatabase(string name, System.IO.Stream databaseStream, System.Collections.Generic.IDictionary<string, System.IO.Stream> attachmentStreams);
    }
}

using System;
using System.IO;
using Couchbase.Lite;
using FluentAssertions;

namespace Test
{
    public class TestCase : IDisposable
    {
        public const string DatabaseName = "testdb";

        protected Database Db { get; private set; }

#if __NET46__
        static TestCase()
        {
            Couchbase.Lite.Support.Net46.Activate();
        }
#endif

        public TestCase()
        {
#if __UWP__
            var dir = Path.Combine(Windows.Storage.ApplicationData.Current.TemporaryFolder.Path, "CouchbaseLite");
#else
            var dir = Path.Combine(Path.GetTempPath(), "CouchbaseLite");
#endif
            Database.Delete(DatabaseName, dir);

            var options = DatabaseOptions.Default;
            options.Directory = dir;
            Db = new Database(DatabaseName, options);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(Db != null) {
                Db.Close();
                Db = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}

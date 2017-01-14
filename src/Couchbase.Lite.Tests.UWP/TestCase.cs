using System;
using System.IO;
using Couchbase.Lite;
using FluentAssertions;
using Windows.Storage;

namespace Test
{
    public class TestCase : IDisposable
    {
        public const string DatabaseName = "testdb";

        protected Database Db { get; private set; }

        public TestCase()
        {
            var dir = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "CouchbaseLite");
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

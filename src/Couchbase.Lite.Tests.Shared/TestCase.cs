using System;
using System.IO;
using Couchbase.Lite;
using FluentAssertions;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Test
{
    public class TestCase : IDisposable
    {
        public const string DatabaseName = "testdb";

        protected Database Db { get; private set; }

        private static string Directory
        {
            get {
                return Path.Combine(Path.GetTempPath(), "CouchbaseLite");
            }
        }

#if __NET46__
        static TestCase()
        {
            Couchbase.Lite.Support.Net46.Activate();
        }
#endif

        public TestCase()
        {
            Database.Delete(DatabaseName, Directory);
            OpenDB();
        }

        protected void OpenDB()
        {
            if(Db != null) {
                throw new InvalidOperationException();
            }

            var options = DatabaseOptions.Default;
            options.Directory = Directory;
            Db = new Database(DatabaseName, options);
            Db.Should().NotBeNull("because otherwise the database failed to open");
        }

        protected void ReopenDB()
        {
            Db.Close();
            Db = null;
            OpenDB();
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

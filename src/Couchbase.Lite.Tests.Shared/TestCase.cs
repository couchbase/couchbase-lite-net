using System;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.DB;
using Couchbase.Lite.Logging;
using FluentAssertions;
using Test.Util;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Test
{
    internal static class Convert
    {
        internal static Document ToConcrete(this IDocument doc)
        {
            return doc as Document;
        }

        internal static Database ToConcrete(this IDatabase db)
        {
            return db as Database;
        }

        internal static Subdocument ToConcrete(this ISubdocument subdoc)
        {
            return subdoc as Subdocument;
        }
    }

    public class TestCase : IDisposable
    {
        public const string DatabaseName = "testdb";
        private readonly ITestOutputHelper _output;

        protected IDatabase Db { get; private set; }

        private static string Directory
        {
            get {
                return Path.Combine(Path.GetTempPath().Replace("cache", "files"), "CouchbaseLite");
            }
        }

#if __NET46__
        static TestCase()
        {
            Couchbase.Lite.Support.Net46.Activate();
        }
#endif

        public TestCase(ITestOutputHelper output)
        {
            Log.AddLogger(new XunitLogger(output));
            _output = output;
            Database.Delete(DatabaseName, Directory);
            OpenDB();
        }

        protected void WriteLine(string line)
        {
            _output.WriteLine(line);
        }

        protected void OpenDB()
        {
            if(Db != null) {
                throw new InvalidOperationException();
            }

            var options = DatabaseOptions.Default;
            options.Directory = Directory;
            Db = DatabaseFactory.Create(DatabaseName, options);
            Db.Should().NotBeNull("because otherwise the database failed to open");
        }

        protected virtual void ReopenDB()
        {
            Db.Dispose();
            Db = null;
            OpenDB();
        }

        protected virtual void Dispose(bool disposing)
        {
            Db?.Dispose();
            Db = null;
            Log.SetDefaultLogger();
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}

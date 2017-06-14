// 
// TestCase.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Logging;
using FluentAssertions;
using Newtonsoft.Json;
using Test.Util;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
#if WINDOWS_UWP
    [TestClass]
#endif
    public class TestCase : IDisposable
    {
        public const string DatabaseName = "testdb";
#if !WINDOWS_UWP
        private readonly ITestOutputHelper _output;
#else
        private TestContext _testContext;
        public TestContext TestContext
        {
            get => _testContext;
            set {
                _testContext = value;
                Log.ClearLoggerProviders();
                Log.AddLoggerProvider(new MSTestLoggerProvider(_testContext));
            }
        }
#endif

        protected Database Db { get; private set; }

        protected IConflictResolver ConflictResolver { get; set; }

        protected static string Directory => Path.Combine(Path.GetTempPath().Replace("cache", "files"), "CouchbaseLite");

#if NETCOREAPP1_0
        static TestCase()
        {
            Couchbase.Lite.Support.NetDestkop.Activate();
        }
#endif

#if !WINDOWS_UWP
        public TestCase(ITestOutputHelper output)
        {
            Log.ClearLoggerProviders();
            Log.AddLoggerProvider(new XunitLoggerProvider(output));
            _output = output;
#else
        public TestCase()
        { 
#endif
            Database.Delete(DatabaseName, Directory);
            OpenDB();
        }

        protected void WriteLine(string line)
        {
#if !WINDOWS_UWP
            _output.WriteLine(line);
#else
            TestContext.WriteLine(line);
#endif
        }

        protected Document SaveDocument(Document document)
        {
            Db.Save(document);
            return Db.GetDocument(document.Id);
        }

        protected Document SaveDocument(Document document, Action<Document> eval)
        {
            eval(document);
            document = SaveDocument(document);
            eval(document);
            return document;
        }

        protected void OpenDB()
        {
            if(Db != null) {
                throw new InvalidOperationException();
            }

            var options = new DatabaseConfiguration {
                Directory = Directory,
                ConflictResolver = ConflictResolver
            };
            Db = new Database(DatabaseName, options);
            Db.Should().NotBeNull("because otherwise the database failed to open");
        }

        protected Database OpenDB(string name)
        {
            var options = new DatabaseConfiguration {
                Directory = Directory
            };

            return new Database(name, options);
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
        }

        protected void LoadJSONResource(string resourceName)
        {
            Db.InBatch(() =>
            {
                var n = 0ul;
                ReadFileByLines($"C/tests/data/{resourceName}.json", line =>
                {
                    var docID = $"doc-{++n:D3}";
                    var json = JsonConvert.DeserializeObject<IDictionary<string, object>>(line);
                    json.Should().NotBeNull("because otherwise the line failed to parse");
                    var doc = new Document(docID);
                    doc.Set(json);
                    Db.Save(doc);

                    return true;
                });
            });
        }

        internal bool ReadFileByLines(string path, Func<string, bool> callback)
        {
#if WINDOWS_UWP
                var url = $"ms-appx:///Assets/{path}";
                var file = Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri(url))
                    .AsTask()
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                var lines = Windows.Storage.FileIO.ReadLinesAsync(file).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                foreach(var line in lines) {
#elif __ANDROID__
            var ctx = global::Couchbase.Lite.Tests.Android.MainActivity.ActivityContext;
            using (var tr = new StreamReader(ctx.Assets.Open(path))) {
                string line;
                while ((line = tr.ReadLine()) != null) {
#elif __IOS__
			var bundlePath = Foundation.NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(path), Path.GetExtension(path));
			using (var tr = new StreamReader(File.Open(bundlePath, FileMode.Open, FileAccess.Read)))
			{
				string line;
				while ((line = tr.ReadLine()) != null)
				{
#else
			using (var tr = new StreamReader(File.Open(path, FileMode.Open)))
			{
				string line;
				while ((line = tr.ReadLine()) != null)
				{
#endif
					if (!callback(line))
					{
						return false;
					}
				}
#if !WINDOWS_UWP
			}
#endif

            return true;
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}

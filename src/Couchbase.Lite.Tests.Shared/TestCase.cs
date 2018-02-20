// 
// TestCase.cs
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
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;

using FluentAssertions;
using Newtonsoft.Json;
using Test.Util;
using LiteCore;
using LiteCore.Interop;
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

        protected static int _counter;
#if !WINDOWS_UWP
        protected readonly ITestOutputHelper _output;
#else
        private TestContext _testContext;
        public TestContext TestContext
        {
            get => _testContext;
            set {
                _testContext = value;
                Log.EnableTextLogging(new MSTestLogger(_testContext));
            }
        }
#endif

        protected Database Db { get; private set; }

        internal IConflictResolver ConflictResolver { get; set; }

        protected static string Directory => Path.Combine(Path.GetTempPath().Replace("cache", "files"), "CouchbaseLite");


#if NETCOREAPP2_0
        static TestCase()
        {
            Couchbase.Lite.Support.NetDesktop.Activate();
        }
#endif


        
#if !WINDOWS_UWP
        public TestCase(ITestOutputHelper output)
        {
            Log.EnableTextLogging(new XunitLogger(output));
            _output = output;
#else
        public TestCase()
        { 
#endif
                
            Database.Delete($"{DatabaseName}{_counter}", Directory);
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


        protected void SaveDocument(MutableDocument document, Action<Document> eval)
        {
            WriteLine("Before Save...");
            eval(document);
            using (var retVal = Db.Save(document)) {
                WriteLine("After Save...");
                eval(retVal);
            }
        }

        protected void LoadNumbers(int num)
        {
            var numbers = new List<IDictionary<string, object>>();
            Db.InBatch(() =>
            {
                for (int i = 1; i <= num; i++) {
                    var docID = $"doc{i}";
                    var doc = new MutableDocument(docID);
                    doc.SetInt("number1", i);
                    doc.SetInt("number2", num - i);
                    Db.Save(doc);
                    numbers.Add(doc.ToDictionary());
                }
            });
        }


        protected void OpenDB()
        {
            if(Db != null) {
                throw new InvalidOperationException();
            }
            
            Db = OpenDB($"{DatabaseName}{_counter}");
        }

        protected Database OpenDB(string name)
        {
            var builder = new DatabaseConfiguration
            {
                Directory = Directory
            };

            if (ConflictResolver != null) {
                builder.ConflictResolver = ConflictResolver;
            }

            return new Database(name, builder);
        }

        protected void ReopenDB()
        {
            Db.Dispose();
            Db = null;
            OpenDB();
        }

        protected virtual void Dispose(bool disposing)
        {
            Db?.Dispose();
            Db = null;
            Log.DisableTextLogging();

            try {
                Database.Delete($"{DatabaseName}{_counter}", Directory);
            } catch (LiteCoreException) {
                // Change the DB Name so that not every single test after this fails,
                // but also fail this test because it didn't clean up properly
                _counter++;
                throw;
            }
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
                    var doc = new MutableDocument(docID);
                    doc.SetData(json);
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

        internal Stream GetTestAsset(string path)
        {
#if WINDOWS_UWP
                var url = $"ms-appx:///Assets/{path}";
                var file = Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri(url))
                    .AsTask()
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                return file.OpenStreamForReadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
#elif __ANDROID__
            var ctx = global::Couchbase.Lite.Tests.Android.MainActivity.ActivityContext;
            return ctx.Assets.Open(path);
#elif __IOS__
			var bundlePath = Foundation.NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(path), Path.GetExtension(path));
			return File.Open(bundlePath, FileMode.Open, FileAccess.Read);
#else
            return File.Open(path, FileMode.Open, FileAccess.Read);
#endif

        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}

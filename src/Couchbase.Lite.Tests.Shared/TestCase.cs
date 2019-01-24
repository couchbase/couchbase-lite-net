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
#if __IOS__
extern alias ios;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;

using FluentAssertions;
using FluentAssertions.Execution;

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
                Database.Log.Custom = new MSTestLogger(_testContext) { Level = LogLevel.Info };
            }
        }
#endif

        protected Database Db { get; private set; }

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
            Database.Log.Custom = new XunitLogger(output) { Level = LogLevel.Info };
            _output = output;
#else
        public TestCase()
        { 
#endif
            var nextCounter = Interlocked.Increment(ref _counter);
            Database.Delete($"{DatabaseName}{nextCounter}", Directory);
            OpenDB(nextCounter);
        }

        protected void WriteLine(string line)
        {
#if !WINDOWS_UWP
            _output.WriteLine(line ?? "<null>");
#else
            TestContext.WriteLine(line ?? "<null>");
#endif
        }


        protected void SaveDocument(MutableDocument document, Action<Document> eval)
        {
            WriteLine("Before Save...");
            eval(document);
            Db.Save(document);
            using (var retVal = Db.GetDocument(document.Id)) {
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
        protected int VerifyQuery(IQuery query, Action<int, Result> block)
        {
            var result = query.Execute();
            using (var e = result.GetEnumerator()) {
                var n = 0;
                while (e.MoveNext()) {
                    block?.Invoke(++n, e.Current);
                }
                return n;
            }
        }

        protected void OpenDB(int count)
        {
            if(Db != null) {
                throw new InvalidOperationException();
            }
            
            Db = OpenDB($"{DatabaseName}{count}");
        }

        protected Database OpenDB(string name)
        {
            var builder = new DatabaseConfiguration
            {
                Directory = Directory
            };

            return new Database(name, builder);
        }

        protected void ReopenDB()
        {
            Db.Dispose();
            Db = OpenDB(Db.Name);
        }

        protected void SaveDocument(MutableDocument document)
        {
            Db.Save(document);

            using (var savedDoc = Db.GetDocument(document.Id)) {
                savedDoc.Id.Should().Be(document.Id);
                if (!TestObjectEquality(document.ToDictionary(), savedDoc.ToDictionary())) {
                    throw new AssertionFailedException($"Expected the saved document to match the original");
                }
            }
        }

        private bool TestObjectEquality(object o1, object o2)
        {
            switch (o1) {
                    case IEnumerable<KeyValuePair<string, object>> e:
                        return TestObjectEquality(e, o2 as IEnumerable<KeyValuePair<string, object>>);
                    case IEnumerable<object> e:
                        return TestObjectEquality(e, o2 as IEnumerable<object>);
                    case byte b:
                    case ushort us:
                    case uint ui:
                    case ulong ul:
                        try {
                            return Equals(Convert.ToUInt64(o1), Convert.ToUInt64(o2));
                        } catch (FormatException) {
                            return false;
                        }
                case sbyte sb:
                    case short s:
                    case int i:
                    case long l:
                    try {
                        return Equals(Convert.ToInt64(o1), Convert.ToInt64(o2));
                    } catch (FormatException) {
                        return false;
                    }
                case float f:
                case double d:
                    try {
                        return Equals(Convert.ToDouble(o1), Convert.ToDouble(o2));
                    } catch (FormatException) {
                        return false;
                    }
                default:
                    return Equals(o1, o2);
            }
        }

        private bool TestObjectEquality(IEnumerable<KeyValuePair<string, object>> dic1, IEnumerable<KeyValuePair<string, object>> dic2)
        {
            if (dic2 == null) {
                return false;
            }

            foreach (var pair in dic1) {
                var second = dic2.FirstOrDefault(x => x.Key.Equals(pair.Key, StringComparison.Ordinal));
                if (String.CompareOrdinal(pair.Key, second.Key) != 0) {
                    throw new AssertionFailedException(
                        $"Expected a dictionary to contain the key {pair.Key} but it didn't");
                }

                if (!TestObjectEquality(pair.Value, second.Value)) {
                    return false;
                }
            }

            return true;
        }

        private bool TestObjectEquality(IEnumerable<object> arr1, IEnumerable<object> arr2)
        {
            if (arr2 == null) {
                return false;
            }

            foreach (var entry in arr1) {
                var second = arr2.FirstOrDefault(x => TestObjectEquality(entry, x));
                if (entry != null && second == null) {
                    return false;
                }
            }

            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
            Db?.Dispose();
            var name = Db?.Name;
            Db = null;
            Database.Log.Custom = null;

            if (name != null) {
                var count = 0;
                do {
                    try {
                        Database.Delete(name, Directory);
                        count = 5;
                    } catch (Exception e) {
                        WriteLine($"Error deleting database: {e.Message}");
                        if (count < 5) {
                            Thread.Sleep(500);                            
                            WriteLine("Retrying...");
                        } else {
                            throw;
                        }
                    }
                } while (count++ < 5);
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

        internal static bool ReadFileByLines(string path, Func<string, bool> callback)
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
			var bundlePath = ios::Foundation.NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(path), Path.GetExtension(path));
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
			var bundlePath = ios::Foundation.NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(path), Path.GetExtension(path));
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

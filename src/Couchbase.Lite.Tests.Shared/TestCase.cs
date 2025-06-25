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
using System.Linq;
using System.Threading;

using Couchbase.Lite;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;

using Shouldly;

using Newtonsoft.Json;
using Test.Util;
using System.Reflection;
using System.Text;

using Newtonsoft.Json.Linq;
using Couchbase.Lite.Internal.Doc;
using Xunit;
using Xunit.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

#if !NET6_0_OR_GREATER && (__ANDROID__ || __IOS__)
namespace System.Diagnostics.CodeAnalysis {
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    public sealed class MemberNotNullAttribute : Attribute
    {
        public MemberNotNullAttribute(params string[] members)
        {
            Members = members;
        }

        public string[] Members { get; }
    }
} 
#endif

namespace Test
{
    public class TestCase : IDisposable
    {
        public const string DatabaseName = "testdb";

        internal static readonly ISelectResult DocID = SelectResult.Expression(Meta.ID);
        internal static readonly ISelectResult Sequence = SelectResult.Expression(Meta.Sequence);
        internal static readonly ISelectResult IsDeleted = SelectResult.Expression(Meta.IsDeleted);
        internal static readonly ISelectResult Expiration = SelectResult.Expression(Meta.Expiration);
        internal static readonly ISelectResult RevID = SelectResult.Expression(Meta.RevisionID);

        private readonly bool _initializing = true;

        protected static int _counter;
        protected readonly ITestOutputHelper _output;

        protected Database Db { get; private set; }

        protected Collection CollA { get; set; }

        protected Collection DefaultCollection => Db.GetDefaultCollection();

        protected static string Directory => Path.Combine(Path.GetTempPath().Replace("cache", "files"), "CouchbaseLite");


#if NET6_0_OR_GREATER && !CBL_NO_VERSION_CHECK && !__ANDROID__ && !__IOS__ && !WINUI
        static TestCase()
        {
            Couchbase.Lite.Support.NetDesktop.CheckVersion();
        }
#endif

        public TestCase(ITestOutputHelper output)
        {
            Database.Log.Custom = new XunitLogger(output) { Level = LogLevel.Info };
            _output = output;
            var nextCounter = Interlocked.Increment(ref _counter);
            Database.Delete($"{DatabaseName}{nextCounter}", Directory);
            Db = OpenNextDB(nextCounter);
            CollA = Db.CreateCollection("CollA");
            _initializing = false;
        }

        protected void WriteLine(string line)
        {
            _output.WriteLine(line ?? "<null>");
        }


        protected void SaveDocument(MutableDocument document, Action<Document> eval)
        {
            WriteLine("Before Save...");
            eval(document);
            DefaultCollection.Save(document);
            using (var retVal = DefaultCollection.GetDocument(document.Id)) {
                retVal.ShouldNotBeNull("because otherwise the save failed");
                WriteLine("After Save...");
                eval(retVal!);
            }
        }

        protected void LoadNumbers(int num)
        {
            var numbers = new List<IDictionary<string, object?>>();
            Db!.InBatch(() =>
            {
                for (int i = 1; i <= num; i++) {
                    var docID = $"doc{i}";
                    var doc = new MutableDocument(docID);
                    doc.SetInt("number1", i);
                    doc.SetInt("number2", num - i);
                    DefaultCollection.Save(doc);
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

        protected Database OpenNextDB(int count)
        {
            if(!_initializing) {
                throw new InvalidOperationException();
            }
            
            return OpenDB($"{DatabaseName}{count}");
        }

        protected Database OpenDB(string name)
        {
            var builder = new DatabaseConfiguration
            {
                Directory = Directory
            };

            return new Database(name, builder);
        }

        [MemberNotNull("Db")]
        protected void ReopenDB()
        {
            Db!.Dispose();
            Db = OpenDB(Db.Name);
        }

        protected void SaveDocument(MutableDocument document)
        {
            DefaultCollection.Save(document);

            using (var savedDoc = DefaultCollection.GetDocument(document.Id)) {
                savedDoc.ShouldNotBeNull("because otherwise the save failed");
                savedDoc!.Id.ShouldBe(document.Id);
                if (!TestObjectEquality(document.ToDictionary(), savedDoc.ToDictionary())) {
                    throw new ShouldAssertException("Expected the saved document to match the original");
                }
            }
        }

        internal void ValidateValuesInMutableDictFromJson(Dictionary<string, object?> dic, IMutableDictionary md)
        {
            foreach (var kvPair in dic) {
                switch (kvPair.Key) {
                    case "nullObj":
                        md.GetValue(kvPair.Key).ShouldBe(kvPair.Value);
                        break;
                    case "byteVal":
                    case "ushortVal":
                    case "uintVal":
                    case "ulongVal":
                        Convert.ToUInt64(md.GetValue(kvPair.Key)).ShouldBe(Convert.ToUInt64(kvPair.Value));
                        break;
                    case "sbyteVal":
                    case "shortVal":
                    case "intVal":
                    case "longVal":
                        Convert.ToInt64(md.GetValue(kvPair.Key)).ShouldBe(Convert.ToInt64(kvPair.Value));
                        break;
                    case "boolVal":
                        md.GetBoolean(kvPair.Key).ShouldBe((bool) kvPair.Value!);
                        break;
                    case "stringVal":
                        md.GetString(kvPair.Key).ShouldBe((string) kvPair.Value!);
                        break;
                    case "floatVal":
                        md.GetFloat(kvPair.Key).ShouldBe((float) kvPair.Value!, 0.0000000001f);
                        break;
                    case "doubleVal":
                        md.GetDouble(kvPair.Key).ShouldBe((double) kvPair.Value!, 0.00001);
                        break;
                    case "dateTimeOffset":
                        md.GetDate(kvPair.Key).ShouldBe((DateTimeOffset) kvPair.Value!);
                        break;
                    case "array":
                        md.GetArray(kvPair.Key).ShouldBeEquivalentTo(new MutableArrayObject((List<int>) kvPair.Value!));
                        md.GetValue(kvPair.Key).ShouldBeEquivalentTo(new MutableArrayObject((List<int>) kvPair.Value!));
                        break;
                    case "dictionary":
                        md.GetDictionary(kvPair.Key).ShouldBeEquivalentTo(new MutableDictionaryObject((Dictionary<string, object?>) kvPair.Value!));
                        md.GetValue(kvPair.Key).ShouldBeEquivalentTo(new MutableDictionaryObject((Dictionary<string, object?>) kvPair.Value!));
                        break;
                    case "blob":
                        md.GetBlob(kvPair.Key).ShouldBeNull("Because we are getting a dictionary represents Blob object back.");
                        var di = ((MutableDictionaryObject?) md.GetValue(kvPair.Key))!.ToDictionary();
                        Blob.IsBlob(di).ShouldBeTrue();
                        di.ShouldBeEquivalentTo(((Blob) dic[kvPair.Key]!).JsonRepresentation);
                        break;
                    default:
                        throw new Exception("This should not happen because all test input values are CBL supported values.");
                }
            }
        }


        internal void ValidateToJsonValues(string json, Dictionary<string, object?> dic)
        {
            var jdic = DataOps.ParseTo<Dictionary<string, object>>(json);
            jdic.ShouldNotBeNull("because otherwise DataOps.ParseTo failed");
            foreach (var i in dic) {
                if (i.Key == "blob") {
                    var b1JsonD = ((JObject) jdic![i.Key]).ToObject<IDictionary<string, object?>>();
                    b1JsonD.ShouldNotBeNull("because otherwise ToObject failed");
                    var b2JsonD = ((Blob?) dic[i.Key])!.JsonRepresentation;

                    foreach (var kv in b2JsonD) {
                        var hasValue = b1JsonD!.TryGetValue(kv.Key, out var gotValue);
                        hasValue.ShouldBeTrue($"because otherwise b1JsonD is missing the key {kv.Key}");
                        gotValue!.ToString().ShouldBe(kv.Value?.ToString());
                    }

                    var blob = new Blob(Db!, b1JsonD!);
                    blob.ShouldBeEquivalentTo((Blob) dic[i.Key]!);
                } else if (i.Key == "floatVal") {
                    (DataOps.ConvertToFloat(jdic![i.Key])).ShouldBe((float) dic[i.Key]!, 0.0000000001f);
                } else {
                    (DataOps.ToCouchbaseObject(jdic![i.Key])).ShouldBeEquivalentTo((DataOps.ToCouchbaseObject(dic[i.Key])));
                }
            }
        }

        internal JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings {
            DateParseHandling = DateParseHandling.DateTimeOffset
        };

        internal static Blob ArrayTestBlob() => new Blob("text/plain", Encoding.UTF8.GetBytes("12345"));

        /// <summary>
        /// dictionary contains CBL supports types:
        /// byte, sbyte, short, ushort, int, uint, long, ulong, float, double, 
        /// bool, string, DateTimeOffset, Blob, a one-dimensional array 
        /// or a dictionary whose members are one of the preceding types.
        /// </summary>
        protected Dictionary<string, object?> PopulateDictData()
        {
            var dt = DateTimeOffset.UtcNow;
            var arr = new List<int> { 1, 2, 3 };
            var dict = new Dictionary<string, object> { ["foo"] = "bar" };
            var blob = ArrayTestBlob();
            Db!.SaveBlob(blob);

            var KeyValueDictionary = new Dictionary<string, object?>()
            {
                { "nullObj", null },
                { "byteVal", (byte) 1 },
                { "sbyteVal", (sbyte) 1 },
                { "ushortVal", (ushort) 1 },
                { "shortVal", (short) 1 },
                { "intVal", 1 },
                { "uintVal", 1U },
                { "longVal", 1L },
                { "ulongVal", 1UL },
                { "boolVal", true },
                { "stringVal", "Test" },
                { "floatVal", 3.14159f },
                { "doubleVal", 1.1 },
                { "dateTimeOffset", dt },
                { "array",  arr},
                { "dictionary", dict },
                { "blob", blob }
            };

            return KeyValueDictionary;
        }

        /// <summary>
        /// list contains CBL supports types:
        /// byte, sbyte, short, ushort, int, uint, long, ulong, float, double, 
        /// bool, string, DateTimeOffset, Blob, a one-dimensional array 
        /// or a dictionary whose members are one of the preceding types.
        /// </summary>
        internal List<object?> PopulateArrayData()
        {
            var dt = DateTimeOffset.UtcNow;
            var arr = new List<int> { 1, 2, 3 };
            var dict = new Dictionary<string, object?> { ["foo"] = "bar" };
            var blob = ArrayTestBlob();
            Db!.SaveBlob(blob);

            var array = new List<object?> {
                null,
                (byte) 1,
                (sbyte) 1,
                (ushort) 1,
                (short) 1,
                1,
                1U,
                1L,
                1UL,
                true,
                "Test",
                3.14159f,
                1.1,
                dt,
                dict,
                arr,
                blob
            };

            return array;
        }

        private bool TestObjectEquality(object? o1, object? o2)
        {
            switch (o1) {
                case IEnumerable<KeyValuePair<string, object?>> e:
                    return TestObjectEquality(e, o2 as IEnumerable<KeyValuePair<string, object?>>);
                case int[] arr:
                    var cnt = arr.Count();
                    var en = o2 as IEnumerable<object>;
                    for (int i = 0; i < cnt; i++) {
                        if (!arr[i].Equals(en?.ElementAt(i))) {
                            return false;
                        }
                    }

                    return true;
                case IEnumerable<object?> e:
                    return TestObjectEquality(e, o2 as IEnumerable<object?>);
                case byte:
                case ushort:
                case uint:
                case ulong:
                    try {
                        return Equals(Convert.ToUInt64(o1), Convert.ToUInt64(o2));
                    } catch (FormatException) {
                        return false;
                    }
                case sbyte:
                case short:
                case int:
                case long:
                    try {
                        return Equals(Convert.ToInt64(o1), Convert.ToInt64(o2));
                    } catch (FormatException) {
                        return false;
                    }
                case float:
                case double:
                    try {
                        return Equals(Convert.ToDouble(o1), Convert.ToDouble(o2));
                    } catch (FormatException) {
                        return false;
                    }
                case DateTimeOffset:
                    try {
                        return Equals(DataOps.ToCouchbaseObject(o1), o2);
                    } catch (FormatException) {
                        return false;
                    }
                default:
                    return Equals(o1, o2);
            }
        }

        private bool TestObjectEquality(IEnumerable<KeyValuePair<string, object?>>? dic1, IEnumerable<KeyValuePair<string, object?>>? dic2)
        {
            if (dic1 == null || dic2 == null) {
                return false;
            }

            foreach (var pair in dic1) {
                var second = dic2.FirstOrDefault(x => x.Key.Equals(pair.Key, StringComparison.Ordinal));
                if (String.CompareOrdinal(pair.Key, second.Key) != 0) {
                    throw new ShouldAssertException(
                        $"Expected a dictionary to contain the key {pair.Key} but it didn't");
                }

                if (!TestObjectEquality(pair.Value, second.Value)) {
                    return false;
                }
            }

            return true;
        }

        private bool TestObjectEquality(IEnumerable<object?>? arr1, IEnumerable<object?>? arr2)
        {
            if (arr1 == null || arr2 == null) {
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
            CollA.Dispose();
            Exception? ex = null;
            var name = Db.Name;
            Db.Close();

            Database.Log.Custom = null;

            var success = Try.Condition(() =>
            {
                try {
                    if(!string.IsNullOrEmpty(name))
                        Database.Delete(name, Directory);
                } catch (Exception e) {
                    ex = e;
                    return false;
                }

                return true;
            }).Times(5).Delay(TimeSpan.FromSeconds(1)).WriteProgress(WriteLine).Go();

            if (!success) {
                throw ex!;
            }
        }

        private void AddPersonInState(string docID, string state, string? firstName = null,
            bool isLegacy = true)
        {
            using (var doc = new MutableDocument(docID)) {
                doc.SetBoolean("custom", true);
                if (!string.IsNullOrEmpty(firstName)) {
                    var nameDict = new MutableDictionaryObject();
                    nameDict.SetString("first", firstName).SetString("last", "lastname");
                    doc.SetDictionary("name", nameDict);
                }

                var addressDoc = new MutableDictionaryObject();
                addressDoc.SetString("state", state);
                var contactDoc = new MutableDictionaryObject();
                contactDoc.SetDictionary("address", addressDoc);
                doc.SetDictionary("contact", contactDoc);

                // Save document:
                if (isLegacy)
                    DefaultCollection.Save(doc);
                else
                    CollA.Save(doc);
            }
        }

        internal static byte[] GetFileByteArray(string filename, Type type)
        {
            byte[]? bytes = null;
#if NET6_0_WINDOWS10 || NET_ANDROID || NET_APPLE
            using (var stream = FileSystem.Current.OpenAppPackageFileAsync(filename).Result)
            using (var memoryStream = new MemoryStream()) {
                stream.CopyTo(memoryStream);
                bytes = memoryStream.ToArray();
            }
#else
            using (var stream = type.GetTypeInfo().Assembly.GetManifestResourceStream(filename))
            using (var sr = new BinaryReader(stream!)) {
                bytes = sr.ReadBytes((int)stream!.Length);
            }
#endif
            return bytes;
        }

#if !CBL_NO_EXTERN_FILES
        protected void TestQueryObserverWithQuery(IQuery query, bool isLegacy = true)
        {
            LoadJSONResource("names_100", coll: isLegacy == true ? null: CollA);
            using (var q = query) {
                var wa = new WaitAssert();
                var wa2 = new WaitAssert();
                var wa3 = new WaitAssert();
                var count = 0;
                q.AddChangeListener(null, (sender, args) =>
                {
                    count++;
                    var list = args.Results.ToList();
                    if (count == 1) { //get init query result
                        wa.RunConditionalAssert(() => list.Count() == 8);
                    } else if (count == 2) { // 1 doc addition
                        wa2.RunConditionalAssert(() => list.Count() == 9);
                    } else { // 1 doc purged
                        wa3.RunConditionalAssert(() => list.Count() == 8);
                    }
                });

                wa.WaitForResult(TimeSpan.FromSeconds(2));
                count.ShouldBe(1, "because we should have received a callback");
                AddPersonInState("after1", "AL", isLegacy: isLegacy);
                Thread.Sleep(2000);
                count.ShouldBe(1, "because we should not receive a callback since AL is not part of query result");
                AddPersonInState("after2", "CA", isLegacy: isLegacy);
                wa2.WaitForResult(TimeSpan.FromSeconds(2));
                count.ShouldBe(2, "because we should have received a callback, query result has updated");
                if(isLegacy)
                    DefaultCollection.Purge("after2");
                else
                    CollA.Purge("after2");

                wa3.WaitForResult(TimeSpan.FromSeconds(2));
                count.ShouldBe(3, "because we should have received a callback, query result has updated");
            }
        }

        protected void TestMultipleQueryObserversWithQuery(IQuery query, bool isLegacy = true)
        {
            LoadJSONResource("names_100", coll: isLegacy == true ? null : CollA);
            using (var q = query)
            using (var q1 = query)
            using (var q2 = query) {
                var wait1 = new ManualResetEventSlim();
                var wait2 = new ManualResetEventSlim();
                var wa2 = new WaitAssert();
                var qCount = 0;
                var q1Count = 0;
                var q2Count = 0;
                int qResultCnt = 0;
                int q1ResultCnt = 0;
                q.AddChangeListener(null, (sender, args) =>
                {
                    qCount++;
                    var list = args.Results.ToList();
                    qResultCnt = list.Count;
                    wait1.Set();
                });

                q1.AddChangeListener(null, (sender, args) =>
                {
                    q1Count++;
                    var list = args.Results.ToList();
                    q1ResultCnt = list.Count;
                    wait2.Set();
                });

                foreach (var handle in new[] { wait1, wait2 }) {
                    handle.Wait(TimeSpan.FromSeconds(2)).ShouldBeTrue();
                }

                qCount.ShouldBe(1, "because we should have received a callback");
                qResultCnt.ShouldBe(8);
                q1Count.ShouldBe(1, "because we should have received a callback");
                q1ResultCnt.ShouldBe(8);
                q2.AddChangeListener(null, (sender, args) =>
                {
                    q2Count++;
                    var list = args.Results.ToList();
                    if (q2Count == 1) { //get init query result
                        wa2.RunConditionalAssert(() => list.Count() == 8);
                    }
                });

                wa2.WaitForResult(TimeSpan.FromSeconds(2));
                q2Count.ShouldBe(1, "because we should have received a callback");
            }
        }

        protected void TestQueryObserverWithChangingQueryParametersWithQuery(IQuery query, bool isLegacy = true)
        {
            LoadJSONResource("names_100", coll: isLegacy ? null : CollA);
            var qParameters = new Parameters().SetString("state", "CA");
            query.Parameters = qParameters;
            //query.Parameters.SetString("state", "CA"); //This works as well
            var wa = new WaitAssert();
            var wa2 = new WaitAssert();
            var count = 0;
            query.AddChangeListener(null, (sender, args) =>
            {
                count++;
                var list = args.Results.ToList();
                if (count == 1) { // get init query result where sate == CA
                    wa.RunConditionalAssert(() => list.Count() == 8);
                } else if (count == 2) { // get query result where sate == NY
                    wa2.RunConditionalAssert(() => list.Count() == 9);
                }
            });

            wa.WaitForResult(TimeSpan.FromSeconds(2));
            count.ShouldBe(1, "because we should have received a callback");
            qParameters.SetString("state", "NY");
            query.Parameters = qParameters;
            //query.Parameters.SetString("state", "NY"); //This works as well
            wa2.WaitForResult(TimeSpan.FromSeconds(2));
            count.ShouldBe(2, "because we should have received a callback, query result has updated");
        }

        protected void LoadJSONResource(string resourceName, Database? db = null, Collection? coll = null)
        {
            if (db == null)
                db = Db!;

            db.InBatch(() =>
            {
                var n = 0ul;
                ReadFileByLines($"C/tests/data/{resourceName}.json", line =>
                {
                    var docID = $"doc-{++n:D3}";
                    var json = JsonConvert.DeserializeObject<IDictionary<string, object>>(line);
                    json.ShouldNotBeNull("because otherwise the line failed to parse");
                    var doc = new MutableDocument(docID);
                    doc.SetData(json!);
                    if(coll == null)
                        db.GetDefaultCollection().Save(doc);
                    else {
                        coll.Save(doc);
                    }

                    return true;
                });
            });
        }

        internal static bool ReadFileByLines(string path, Func<string, bool> callback)
        {
#if NET6_0_WINDOWS10 || NET_ANDROID || NET_APPLE
            using(var stream = FileSystem.Current.OpenAppPackageFileAsync(path.Replace("C/tests/data/", "")).Result)
            using (var tr = new StreamReader(stream)) { 
                string line;
				while ((line = tr.ReadLine()) != null) {
#elif __ANDROID__ && !NET_ANDROID
            var ctx = global::Couchbase.Lite.Tests.Android.MainActivity.ActivityContext;
            using (var tr = new StreamReader(ctx.Assets.Open(path))) {
                string line;
                while ((line = tr.ReadLine()) != null) {
#elif __IOS__
			var bundlePath = Foundation.NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(path), Path.GetExtension(path));
			using (var tr = new StreamReader(File.Open(bundlePath, FileMode.Open, FileAccess.Read))) {
				string line;
				while ((line = tr.ReadLine()) != null) {
#else
            using (var tr = new StreamReader(typeof(TestCase).GetTypeInfo().Assembly!.GetManifestResourceStream(path.Replace("C/tests/data/", ""))!)) {
				string? line;
				while ((line = tr.ReadLine()) != null) {
#endif
                    if (!callback(line)) {
                        return false;
                    }
				}
            }

            return true;
        }

        internal Stream GetTestAsset(string path)
        {
#if NET6_0_WINDOWS10 || NET_APPLE || NET_ANDROID
            return FileSystem.Current.OpenAppPackageFileAsync(path).Result;
#elif __ANDROID__ && !NET_ANDROID
            var ctx = global::Couchbase.Lite.Tests.Android.MainActivity.ActivityContext;
            return ctx.Assets.Open(path);
#elif __IOS__ && !NET_APPLE
            var bundlePath = Foundation.NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(path), Path.GetExtension(path));
			return File.Open(bundlePath, FileMode.Open, FileAccess.Read);
#elif WINUI
            return File.Open(path.Replace("replacedb/", ""), FileMode.Open, FileAccess.Read);
#else
            return File.Open(path, FileMode.Open, FileAccess.Read);
#endif

        }
#endif

        public void Dispose()
        {
            Dispose(true);
        }
    }
}

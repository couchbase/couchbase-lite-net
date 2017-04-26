//
//  DocumentTest.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;
using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public class DocumentTest : TestCase
    {
        private Document _doc;

#if !WINDOWS_UWP
        public DocumentTest(ITestOutputHelper output) : base(output)
#else
        public DocumentTest()
#endif
        {
            Db.ConflictResolver = new DoNotResolve();
            _doc = Db["doc1"];
        }

        [Fact]
        public void TestNewDoc()
        {
            var doc = new Document();
            doc.Id.Should().NotBeNullOrEmpty("because a document should always have an ID");
            doc.Exists.Should().BeFalse("because the document has not been saved yet");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");

            doc.ToDictionary().Should().BeEmpty("because no properties have been added");
            doc.GetObject("prop").Should().BeNull("because this property does not exist");
            doc.GetBoolean("prop").Should().BeFalse("because this bool does not exist");
            doc.GetInt("prop").Should().Be(0, "because this int does not exist");
            doc.GetLong("prop").Should().Be(0L, "because this long does not exist");
            doc.GetDouble("prop").Should().BeApproximately(0.0, Double.Epsilon, "because this double does not exist");
            doc.GetDate("prop").Should().Be(DateTimeOffset.MinValue, "because this date does not exist");
            doc.GetString("prop").Should().BeNull("because this string does not exist");
            doc.GetBlob("prop").Should().BeNull("because this blob does not exist");

            Db.Save(doc);
        }

        [Fact]
        public void TestNewDocWithID()
        {
            var doc =  new Document("doc-a");
            doc.Id.Should().Be("doc-a", "because that is the ID that was passed");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");

            doc.ToDictionary().Should().BeEmpty("because no properties have been added");
            doc.GetObject("prop").Should().BeNull("because this property does not exist");
            doc.GetBoolean("prop").Should().BeFalse("because this bool does not exist");
            doc.GetInt("prop").Should().Be(0, "because this int does not exist");
            doc.GetLong("prop").Should().Be(0L, "because this long does not exist");
            doc.GetDouble("prop")
                .Should()
                .BeApproximately(0.0, Double.Epsilon, "because this double does not exist");
            doc.GetDate("prop").Should().Be(DateTimeOffset.MinValue, "because this date does not exist");
            doc.GetString("prop").Should().BeNull("because this string does not exist");
            doc.GetBlob("prop").Should().BeNull("because this blob does not exist");

            Db.Save(doc);
        }

        [Fact]
        public void TestPropertyAccessors()
        {
            var date = DateTimeOffset.Now;
            var doc = Db.GetDocument("doc1");
            doc.Set("yes", true)
                .Set("no", false)
                .Set("double", 1.1)
                .Set("integer", 2)
                .Set("zero", 0)
                .Set("string", "str")
                .Set("dict", new Dictionary<string, object> {["foo"] = "bar"})
                .Set("array", new[] {"1", "2"})
                .Set("date", date);


            var subdoc = new Subdocument();
            subdoc.Set("firstname", "scottie")
                .Set("lastname", "zebra");

            doc.Set("subdoc", subdoc);
            Db.Save(doc);

            doc.GetBoolean("yes").Should().BeTrue("because that is the bool that was saved");
            doc.GetBoolean("no").Should().BeFalse("because that is the bool that was saved");
            doc.GetDouble("double").Should().BeApproximately(1.1, Double.Epsilon, "because that is the double that was saved");
            doc.GetInt("integer").Should().Be(2, "because that is the integer that was saved");
            doc.GetInt("zero").Should().Be(0, "because that is the integer that was saved");

            doc.GetString("string").Should().Be("str", "because that is the string that was saved");
            doc.GetArray("array").ToList().ShouldBeEquivalentTo(new[] { "1", "2" }, "because that is the array that was saved");
            doc.GetSubdocument("dict")
                .ToDictionary()
                .ShouldBeEquivalentTo(new Dictionary<string, object> {["foo"] = "bar"},
                    "because the is the dictionary that was saved");
            doc.GetDate("date").Should().Be(date, "because that is the date that was saved");

            subdoc = doc.GetSubdocument("subdoc");
            subdoc.Should().NotBeNull("because a subdocument was saved into this document for this key");
            subdoc.GetString("firstname").Should().Be("scottie", "because that it the first name that was saved");
            subdoc.GetString("lastname").Should().Be("zebra", "because that is the last name that was saved");

            // Reopen the database and get the document again

            ReopenDB();

            doc.GetBoolean("yes").Should().BeTrue("because that is the bool that was saved");
            doc.GetBoolean("no").Should().BeFalse("because that is the bool that was saved");
            doc.GetDouble("double").Should().BeApproximately(1.1, Double.Epsilon, "because that is the double that was saved");
            doc.GetInt("integer").Should().Be(2, "because that is the integer that was saved");
            doc.GetInt("zero").Should().Be(0, "because that is the integer that was saved");

            doc.GetString("string").Should().Be("str", "because that is the string that was saved");
            doc.GetArray("array").ToList().ShouldBeEquivalentTo(new[] { "1", "2" }, "because that is the array that was saved");
            doc.GetSubdocument("dict")
                .ToDictionary()
                .ShouldBeEquivalentTo(new Dictionary<string, object> { ["foo"] = "bar" },
                    "because the is the dictionary that was saved");
            doc.GetDate("date").Should().Be(date, "because that is the date that was saved");

            subdoc = doc.GetSubdocument("subdoc");
            subdoc.Should().NotBeNull("because a subdocument was saved into this document for this key");
            subdoc.GetString("firstname").Should().Be("scottie", "because that it the first name that was saved");
            subdoc.GetString("lastname").Should().Be("zebra", "because that is the last name that was saved");
        }

        [Fact]
        public void TestRemoveKeys()
        {
            _doc.Set(new Dictionary<string, object> {
                ["type"] = "profile",
                ["name"] = "Jason",
                ["weight"] = 130.5,
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 milky way.",
                    ["city"] = "galaxy city",
                    ["zip"] = 12345
                }
            });

            Db.Save(_doc);
            _doc.Set("name", null);
            _doc.Set("weight", null);
            _doc.Set("age", null);
            _doc.Set("active", null);
            _doc.GetSubdocument("address").Set("city", null);

            _doc.GetString("name").Should().BeNull("because it was removed");
            _doc.GetDouble("weight").Should().Be(0.0, "because it was removed");
            _doc.GetLong("age").Should().Be(0L, "because it was removed");
            _doc.GetBoolean("active").Should().BeFalse("because it was removed");

            _doc.GetObject("name").Should().BeNull("because it was removed");
            _doc.GetObject("weight").Should().BeNull("because it was removed");
            _doc.GetObject("age").Should().BeNull("because it was removed");
            _doc.GetObject("active").Should().BeNull("because it was removed");
            _doc.GetSubdocument("address").GetString("city").Should().BeNull("because it was removed");

            var address = _doc.GetSubdocument("address");
            _doc.ToDictionary().ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["type"] = "profile",
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 milky way.",
                    ["zip"] = 12345L
                }
            });
            address.ToDictionary().ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["street"] = "1 milky way.",
                ["zip"] = 12345L
            });

            // Remove the rest:
            _doc.Set("type", null);
            _doc.Set("address", null);
            _doc.ToDictionary().Should().BeEmpty("because everything was removed");
        }

        [Fact]
        public void TestContainsKey()
        {
            var doc = Db["doc1"];
            doc.Set(new Dictionary<string, object> {
                ["type"] = "profile",
                ["name"] = "Jason",
                ["age"] = 30,
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 milky way."
                }
            });

            doc.Contains("type").Should().BeTrue("because 'type' exists in the document");
            doc.Contains("name").Should().BeTrue("because 'name' exists in the document");
            doc.Contains("address").Should().BeTrue("because 'address' exists in the document");
            doc.Contains("weight").Should().BeFalse("because 'weight' does not exist in the document");
        }

        [Fact]
        public void TestDelete()
        {
            _doc.Set("type", "profile");
            _doc.Set("name", "Scott");
            _doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

            // Delete before save:
            Db.Invoking(d => d.Delete(_doc)).ShouldThrow<LiteCoreException>().Which.Error.Should()
                .Be(new C4Error(LiteCoreError.NotFound), "because an attempt to delete a non-existent document was made");

            _doc["type"].ToString().Should().Be("profile", "because the doc should still exist");
            _doc["name"].ToString().Should().Be("Scott", "because the doc should still exist");

            // Save:
            Db.Save(_doc);
            _doc.IsDeleted.Should().BeFalse("beacuse the document is still not deleted");

            // Delete:
            Db.Delete(_doc);
            _doc.IsDeleted.Should().BeTrue("because now the document is deleted");
            _doc.ToDictionary().Should().BeEmpty("because a deleted document has no properties");
        }

        [Fact]
        public void TestPurge()
        {
            _doc.Set("type", "profile");
            _doc.Set("name", "Scott");
            _doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");
            _doc.Exists.Should().BeFalse("because the document has not been saved yet");
            _doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

            // Purge before save:
            Db.Purge(_doc).Should().BeFalse("because deleting a non-existent document is invalid");
            _doc["type"].ToString().Should().Be("profile", "because the doc should still exist");
            _doc["name"].ToString().Should().Be("Scott", "because the doc should still exist");

            // Save:
            Db.Save(_doc);
            _doc.IsDeleted.Should().BeFalse("beacuse the document is still not deleted");

            // Purge:
            Db.Purge(_doc).Should().BeTrue("because the purge should succeed now");
            _doc.IsDeleted.Should().BeFalse("because the document does not exist");
        }

        [Fact]
        public void TestReopenDB()
        {
            _doc.Set("string", "str");
            Db.Save(_doc);

            ReopenDB();

            _doc = Db["doc1"];
            _doc.ToDictionary().Should().Equal(new Dictionary<string, object> { ["string"] = "str" }, "because otherwise the property didn't get saved");
            _doc["string"].ToString().Should().Be("str", "because otherwise the property didn't get saved");
        }

        [Fact]
        public void TestConflict()
        {
            Db.ConflictResolver = new TheirsWins();
            var doc = SetupConflict();
            Db.Save(doc);
            doc["name"].ToString().Should().Be("Scotty", "because the 'theirs' version should win");

            doc = new Document("doc2");
            Db.ConflictResolver = new MergeThenTheirsWins();
            doc.Set("type", "profile");
            doc.Set("name", "Scott");
            Db.Save(doc);

            // Force a conflict again
            var properties = doc.ToDictionary();
            properties["type"] = "bio";
            properties["gender"] = "male";
            SaveProperties(properties, doc.Id);

            // Save and make sure that the correct conflict resolver won
            doc.Set("type", "bio");
            doc.Set("age", 31);
            Db.Save(doc);

            doc["age"].ToLong().Should().Be(31L, "because 'age' was changed by 'mine' and not 'theirs'");
            doc["type"].ToString().Should().Be("bio", "because 'type' was changed by 'mine' and 'theirs' so 'theirs' should win");
            doc["gender"].ToString().Should().Be("male", "because 'gender' was changed by 'theirs' but not 'mine'");
            doc["name"].ToString().Should().Be("Scott", "because 'name' was unchanged");
        }

        [Fact]
        public void TestConflictResolverGivesUp()
        {
            Db.ConflictResolver = new GiveUp();
            var doc = SetupConflict();
            Db.Invoking(d => d.Save(doc))
                .ShouldThrow<CouchbaseLiteException>()
                .Which.Code.Should()
                .Be(StatusCode.Conflict, "because the conflict resolver gave up");
        }

        [Fact]
        public void TestDeletionConflict()
        {
            Db.ConflictResolver = new DoNotResolve();
            var doc = SetupConflict();
            Db.Delete(doc);
            doc.Exists.Should().BeTrue("because there was a conflict in place of thgie deletion");
            doc.IsDeleted.Should().BeFalse("because there was a conflict in place of the deletion");
            doc["name"].ToString().Should().Be("Scotty", "because that was the pre-deletion value");
        }

        [Fact]
        public void TestConflictMineIsDeeper()
        {
            Db.ConflictResolver = null;
            var doc = SetupConflict();
            Db.Save(doc);
            doc["name"].ToString().Should().Be("Scott Pilgrim", "because the current in memory document has a longer history");
        }

        [Fact]
        public void TestConflictTheirsIsDeeper()
        {
            Db.ConflictResolver = null;
            var doc = SetupConflict();

            // Add another revision to the conflict, so it'll have a higher generation
            var properties = doc.ToDictionary();
            properties["name"] = "Scott of the Sahara";
            SaveProperties(properties, doc.Id);
            Db.Save(doc);

            doc["name"].ToString().Should().Be("Scott of the Sahara", "because the conflict has a longer history");
        }

        [Fact]
        public void TestBlob()
        {
            var content = Encoding.UTF8.GetBytes("12345");
            var data = new Blob("text/plain", content);
            _doc.Set("data", data);
            _doc.Set("name", "Jim");
            Db.Save(_doc);

            using(var otherDb = new Database(Db.Name, Db.Config)) {
                var doc1 = otherDb["doc1"];
                doc1["name"].ToString().Should().Be("Jim", "because the document should be persistent after save");
                doc1["data"].Value.Should().BeAssignableTo<Blob>("because otherwise the data did not save correctly");
                data = doc1.GetBlob("data");

                data.Length.Should().Be(5, "because the data is 5 bytes long");
                data.Content.Should().Equal(content, "because the data should have been retrieved correctly");
                var contentStream = data.ContentStream;
                var buffer = new byte[10];
                var bytesRead = contentStream.Read(buffer, 0, 10);
                contentStream.Dispose();
                bytesRead.Should().Be(5, "because the data is 5 bytes long");
            }
        }

        [Fact]
        public void TestEmptyBlob()
        {
            var content = new byte[0];
            var data = new Blob("text/plain", content);
            _doc.Set("data", data);
            Db.Save(_doc);

            using(var otherDb = new Database(Db.Name, Db.Config)) {
                var doc1 = otherDb["doc1"];
                doc1["data"].Value.Should().BeAssignableTo<Blob>("because otherwise the data did not save correctly");
                data = doc1.GetBlob("data");

                data.Length.Should().Be(0, "because the data is 5 bytes long");
                data.Content.Should().Equal(content, "because the data should have been retrieved correctly");
                var contentStream = data.ContentStream;
                var buffer = new byte[10];
                var bytesRead = contentStream.Read(buffer, 0, 10);
                contentStream.Dispose();
                bytesRead.Should().Be(0, "because the data is 5 bytes long");
            }
        }

        [Fact]
        public void TestBlobWithStream()
        {
            var content = new byte[0];
            Stream contentStream = new MemoryStream(content);
            var data = new Blob("text/plain", contentStream);
            _doc.Set("data", data);
            Db.Save(_doc);

            using(var otherDb = new Database(Db.Name, Db.Config)) {
                var doc1 = otherDb["doc1"];
                doc1["data"].Value.Should().BeAssignableTo<Blob>("because otherwise the data did not save correctly");
                data = doc1.GetBlob("data");

                data.Length.Should().Be(0, "because the data is 5 bytes long");
                data.Content.Should().Equal(content, "because the data should have been retrieved correctly");
                contentStream = data.ContentStream;
                var buffer = new byte[10];
                var bytesRead = contentStream.Read(buffer, 0, 10);
                contentStream.Dispose();
                bytesRead.Should().Be(0, "because the data is 5 bytes long");
            }
        }

        [Fact]
        public void TestMultipleBlobRead()
        {
            var content = Encoding.UTF8.GetBytes("12345");
            var data = new Blob("text/plain", content);
            _doc.Set("data", data);
            data = _doc.GetBlob("data");
            for (int i = 0; i < 5; i++) {
                data.Content.Should().Equal(content, "because otherwise incorrect data was read");
                using (var contentStream = data.ContentStream) {
                    var buffer = new byte[10];
                    var bytesRead = contentStream.Read(buffer, 0, 10);
                    bytesRead.Should().Be(5, "because the data has 5 bytes");
                }
            }

            Db.Save(_doc);
            
            using(var otherDb = new Database(Db.Name, Db.Config)) {
                var doc1 = otherDb["doc1"];
                doc1["data"].Value.Should().BeAssignableTo<Blob>("because otherwise the data did not save correctly");
                data = doc1.GetBlob("data");

                data.Length.Should().Be(5, "because the data is 5 bytes long");
                data.Content.Should().Equal(content, "because the data should have been retrieved correctly");
                var contentStream = data.ContentStream;
                var buffer = new byte[10];
                var bytesRead = contentStream.Read(buffer, 0, 10);
                contentStream.Dispose();
                bytesRead.Should().Be(5, "because the data is 5 bytes long");
            }
        }

        [Fact]
        public void TestReadExistingBlob()
        {
            var content = Encoding.UTF8.GetBytes("12345");
            var data = new Blob("text/plain", content);
            _doc.Set("data", data);
            _doc.Set("name", "Jim");
            Db.Save(_doc);

            ReopenDB();

            _doc = Db["doc1"];
            _doc.GetBlob("data").Content.Should().Equal(content, "because the data should have been retrieved correctly");

            ReopenDB();

            _doc = Db["doc1"];
            _doc.Set("foo", "bar");
            Db.Save(_doc);
            _doc.GetBlob("data").Content.Should().Equal(content, "because the data should have been retrieved correctly");
        }

        private Document SetupConflict()
        {
            _doc.Set("type", "profile");
            _doc.Set("name", "Scott");
            Db.Save(_doc);

            // Force a conflict
            var properties = _doc.ToDictionary();
            properties["name"] = "Scotty";
            SaveProperties(properties, _doc.Id);

            _doc.Set("name", "Scott Pilgrim");
            return _doc;
        }

        private unsafe void SaveProperties(IDictionary<string, object> props, string docID)
        {
            Db.InBatch(() =>
            {
                var tricky =
                    (C4Document*) LiteCoreBridge.Check(err => Native.c4doc_get(Db.c4db, docID, true, err));
                var put = new C4DocPutRequest {
                    docID = tricky->docID,
                    history = &tricky->revID,
                    historyCount = 1,
                    save = true
                };

                var body = Db.JsonSerializer.Serialize(props);
                put.body = body;

                LiteCoreBridge.Check(err =>
                {
                    var localPut = put;
                    var retVal = Native.c4doc_put(Db.c4db, &localPut, null, err);
                    Native.FLSliceResult_Free(body);
                    return retVal;
                });
            });
        }
    }

    internal class TheirsWins : IConflictResolver
    {
        public ReadOnlyDocument Resolve(Conflict conflict)
        {
            return conflict.Target;
        }
    }

    internal class MergeThenTheirsWins : IConflictResolver
    {
        public ReadOnlyDocument Resolve(Conflict conflict)
        {
            var resolved = new Document(conflict.CommonAncestor.ToDictionary());
            var changed = new HashSet<string>();
            foreach(var pair in conflict.Target) {
                resolved.Set(pair.Key, pair.Value);
                changed.Add(pair.Key);
            }

            foreach(var pair in conflict.Source) {
                if(!changed.Contains(pair.Key)) {
                    resolved.Set(pair.Key, pair.Value);
                }
            }

            return resolved;
        }
    }

    internal class GiveUp : IConflictResolver
    {
        public ReadOnlyDocument Resolve(Conflict conflict)
        {
            return null;
        }
    }

    internal class DoNotResolve : IConflictResolver
    {
        public ReadOnlyDocument Resolve(Conflict conflict)
        {
            throw new NotImplementedException();
        }
    }
}

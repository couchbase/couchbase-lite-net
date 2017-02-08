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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Support;
using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
using Xunit;

namespace Test
{
    public class DocumentTest : TestCase
    {
        private IDocument _doc;
        private IDispatchQueue _docQueue;

        public DocumentTest()
        {
            Db.ActionQueue.DispatchSync(() =>
            {
                Db.ConflictResolver = new DoNotResolve();
                _doc = Db["doc1"];
                _docQueue = _doc.ActionQueue;
            });
        }

        [Fact]
        public async Task TestNewDoc()
        {
            var doc = await Db.ActionQueue.DispatchAsync(() => Db.CreateDocument());
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc.Id.Should().NotBeNullOrEmpty("because a document should always have an ID");
                doc.Database.Should().BeSameAs(Db, "because a doc should have a reference to its owner");
                doc.Exists.Should().BeFalse("because the document has not been saved yet");
                doc.IsDeleted.Should().BeFalse("because the document is not deleted");
                doc.Properties.Should().BeEmpty("because no properties have been added");
                doc["prop"].Should().BeNull("because this property does not exist");
                doc.GetBoolean("prop").Should().BeFalse("because this bool does not exist");
                doc.GetLong("prop").Should().Be(0L, "because this int does not exist");
                doc.GetFloat("prop").Should().BeApproximately(0.0f, Single.Epsilon, "because this float does not exist");
                doc.GetDouble("prop").Should().BeApproximately(0.0, Double.Epsilon, "because this double does not exist");
                doc.GetDate("prop").Should().BeNull("because this date does not exist");
                doc.GetString("prop").Should().BeNull("because this string does not exist");

                doc.Save();
                doc.Exists.Should().BeTrue("because the document was saved");
                doc.IsDeleted.Should().BeFalse("because the document is not deleted");
                doc.Properties.Should().BeEmpty("because no properties were added");
            });
        }

        [Fact]
        public async Task TestNewDocWithID()
        {
            var doc = await Db.ActionQueue.DispatchAsync(() => Db.GetDocument("doc1"));
            Db.ActionQueue.DispatchSync(() => Db.GetDocument("doc1")).Should().BeSameAs(doc, "because the document should be cached");
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc.Id.Should().Be("doc1", "because that is the ID that was passed");
                doc.Database.Should().BeSameAs(Db, "because a doc should have a reference to its owner");
                doc.Exists.Should().BeFalse("because the document has not been saved yet");
                doc.IsDeleted.Should().BeFalse("because the document is not deleted");
                doc.Properties.Should().BeEmpty("because no properties have been added");
                doc["prop"].Should().BeNull("because this property does not exist");
                doc.GetBoolean("prop").Should().BeFalse("because this bool does not exist");
                doc.GetLong("prop").Should().Be(0L, "because this int does not exist");
                doc.GetFloat("prop").Should().BeApproximately(0.0f, Single.Epsilon, "because this float does not exist");
                doc.GetDouble("prop").Should().BeApproximately(0.0, Double.Epsilon, "because this double does not exist");
                doc.GetDate("prop").Should().BeNull("because this date does not exist");
                doc.GetString("prop").Should().BeNull("because this string does not exist");

                doc.Save();
                doc.Exists.Should().BeTrue("because the document was saved");
                doc.IsDeleted.Should().BeFalse("because the document is not deleted");
                doc.Properties.Should().BeEmpty("because no properties were added");
            });
            Db.ActionQueue.DispatchSync(() => Db.GetDocument("doc1")).Should().BeSameAs(doc, "because the document should be cached");
        }

        [Fact]
        public async Task TestPropertyAccessors()
        {
            var doc = await Db.ActionQueue.DispatchAsync(() => Db.GetDocument("doc1"));
            var date = DateTimeOffset.Now;
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc.Set("bool", true)
                    .Set("double", 1.1)
                    .Set("float", 1.2f)
                    .Set("integer", 2L)
                    .Set("string", "str")
                    //.Set("dict", new Dictionary<string, object> { ["foo"] = "bar" })
                    .Set("array", new[] { "1", "2" })
                    .Set("date", date)
                    .Save();

                doc.GetBoolean("bool").Should().BeTrue("because that is the bool that was saved");
                doc.GetDouble("double").Should().BeApproximately(1.1, Double.Epsilon, "because that is the double that was saved");
                doc.GetFloat("float").Should().BeApproximately(1.2f, Single.Epsilon, "because that is the float that was saved");
                doc.GetLong("integer").Should().Be(2L, "because that is the integer that was saved");

                doc.GetString("string").Should().Be("str", "because that is the string that was saved");
                //doc.Get("dict").ShouldBeEquivalentTo(new Dictionary<string, object> { ["foo"] = "bar" }, "because that is the dict that was saved");
                doc.Get("array").ShouldBeEquivalentTo(new[] { "1", "2" }, "because that is the array that was saved");

                doc.GetDate("date").Should().Be(date, "because that is the date that was saved");
            });

            // Get the doc from another database
            using(var otherDB = DatabaseFactory.Create(Db)) {
                var doc1 = await otherDB.ActionQueue.DispatchAsync(() => otherDB.GetDocument("doc1"));
                await doc1.ActionQueue.DispatchAsync(() =>
                {
                    doc1.GetBoolean("bool").Should().BeTrue("because that is the bool that was saved");
                    doc1.GetDouble("double").Should().BeApproximately(1.1, Double.Epsilon, "because that is the double that was saved");
                    doc1.GetFloat("float").Should().BeApproximately(1.2f, Single.Epsilon, "because that is the float that was saved");
                    doc1.GetLong("integer").Should().Be(2L, "because that is the integer that was saved");

                    doc1.GetString("string").Should().Be("str", "because that is the string that was saved");
                    //doc1.Get("dict").ShouldBeEquivalentTo(new Dictionary<string, object> { ["foo"] = "bar" }, "because that is the dict that was saved");
                    doc1.Get("array").ShouldBeEquivalentTo(new[] { "1", "2" }, "because that is the array that was saved");

                    doc1.GetDate("date").Should().Be(date, "because that is the date that was saved");
                });
            }
        }

        [Fact]
        public async Task TestProperties()
        {
            var doc = await Db.ActionQueue.DispatchAsync(() => Db["doc1"]);
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc["type"] = "demo";
                doc["weight"] = 12.5;
                doc["tags"] = new[] { "useless", "emergency" };

                doc["type"].Should().Be("demo", "because that is the type that was entered");
                doc["weight"].As<double>().Should().BeApproximately(12.5, Double.Epsilon, "beacuse that is the weight that was entered");
                doc.Properties.Should().Contain("type", "demo").And.Contain("weight", 12.5, "because those simple values were added");
                doc.Properties["tags"].As<IEnumerable<string>>().Should()
                    .ContainInOrder(new[] { "useless", "emergency" }, "because those were the tags that were added");
            });
        }

        [Fact]
        public async Task TestRemoveProperties()
        {
            await _docQueue.DispatchAsync(() =>
            {
                _doc.Properties = new Dictionary<string, object> {
                    ["type"] = "profile",
                    ["name"] = "Jason",
                    ["weight"] = 130.5,
                    ["address"] = new Dictionary<string, object> {
                        ["street"] = "1 milky way.",
                        ["city"] = "galaxy city",
                        ["zip"] = 12345
                    }
                };

                _doc.GetDouble("weight").Should().BeApproximately(130.5, Double.Epsilon, "because that is the value that was entered");
                (_doc["address"] as IDictionary<string, object>)["city"].Should().Be("galaxy city", "because that is the value that was entered");

                _doc["name"] = null;
                _doc["weight"] = null;
                (_doc["address"] as IDictionary<string, object>)["city"] = null;
                _doc["name"].Should().BeNull("because it was removed");
                _doc["weight"].Should().BeNull("because it was removed");
                _doc.GetDouble("weight").Should().Be(0.0, "because that is the default double value");
                (_doc["address"] as IDictionary<string, object>)["city"].Should().BeNull("because it was removed");
            });
        }

        [Fact]
        public async Task TestContainsKey()
        {
            var doc = await Db.ActionQueue.DispatchAsync(() => Db["doc1"]);
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc.Properties = new Dictionary<string, object> {
                    ["type"] = "profile",
                    ["name"] = "Jaon",
                    ["address"] = new Dictionary<string, object> {
                        ["street"] = "1 milky way."
                    }
                };

                doc.Contains("type").Should().BeTrue("because 'type' exists in the document");
                doc.Contains("name").Should().BeTrue("because 'name' exists in the document");
                doc.Contains("address").Should().BeTrue("because 'address' exists in the document");
                doc.Contains("weight").Should().BeFalse("because 'weight' does not exist in the document");
            });
        }

        [Fact]
        public async Task TestDelete()
        {
            var doc = await Db.ActionQueue.DispatchAsync(() => Db["doc1"]);
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc["type"] = "Profile";
                doc["name"] = "Scott";
                doc.Exists.Should().BeFalse("because the document has not been saved yet");
                doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

                // Delete before save:
                doc.Invoking(d => d.Delete()).ShouldThrow<LiteCoreException>().Which.Error.Should()
                    .Be(new C4Error(LiteCoreError.NotFound), "because an attempt to delete a non-existent document was made");

                doc["type"].Should().Be("Profile", "because the doc should still exist");
                doc["name"].Should().Be("Scott", "because the doc should still exist");

                // Save:
                doc.Save();
                doc.Exists.Should().BeTrue("because the document was saved");
                doc.IsDeleted.Should().BeFalse("beacuse the document is still not deleted");

                // Delete:
                doc.Delete();
                doc.Exists.Should().BeTrue("because the document still exists in terms of the DB");
                doc.IsDeleted.Should().BeTrue("because now the document is deleted");
                doc.Properties.Should().BeEmpty("because a deleted document has no properties");
            });
        }

        [Fact]
        public async Task TestPurge()
        {
            var doc = await Db.ActionQueue.DispatchAsync(() => Db["doc1"]);
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc["type"] = "Profile";
                doc["name"] = "Scott";
                doc.Exists.Should().BeFalse("because the document has not been saved yet");
                doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

                // Purge before save:
                doc.Purge().Should().BeFalse("because deleting a non-existent document is invalid");
                doc["type"].Should().Be("Profile", "because the doc should still exist");
                doc["name"].Should().Be("Scott", "because the doc should still exist");

                // Save:
                doc.Save();
                doc.Exists.Should().BeTrue("because the document was saved");
                doc.IsDeleted.Should().BeFalse("beacuse the document is still not deleted");

                // Purge:
                doc.Purge().Should().BeTrue("because the purge should succeed now");
                doc.Exists.Should().BeFalse("because the document was blown away");
                doc.IsDeleted.Should().BeFalse("because the document does not exist");
                doc.Properties.Should().BeEmpty("because a purged document has no properties");
            });
        }

        [Fact]
        public async Task TestRevert()
        {
            var doc = await Db.ActionQueue.DispatchAsync(() => Db["doc1"]);
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc["type"] = "Profile";
                doc["name"] = "Scott";

                // Reset before save:
                doc.Revert();
                doc["type"].Should().BeNull("because the document was reset");
                doc["name"].Should().BeNull("because the document was reset");

                // Save:
                doc["type"] = "Profile";
                doc["name"] = "Scott";
                doc.Save();
                doc["type"].Should().Be("Profile", "because the save completed");
                doc["name"].Should().Be("Scott", "because the save completed");

                // Make some changes:
                doc["type"] = "user";
                doc["name"] = "Scottie";

                // Reset:
                doc.Revert();
                doc["type"].Should().Be("Profile", "because the document was reset");
                doc["name"].Should().Be("Scott", "because the document was reset");
            });
        }

        [Fact]
        public async Task TestReopenDB()
        {
            var doc = await Db.ActionQueue.DispatchAsync(() => Db["doc1"]);
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc["string"] = "str";
                doc.Properties.Should().Equal(new Dictionary<string, object> { ["string"] = "str" }, "because otherwise the property didn't get inserted");
                doc.Save();
            });

            ReopenDB();

            doc = await Db.ActionQueue.DispatchAsync(() => Db["doc1"]);
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc.Properties.Should().Equal(new Dictionary<string, object> { ["string"] = "str" }, "because otherwise the property didn't get saved");
                doc["string"].Should().Be("str", "because otherwise the property didn't get saved");
            });
        }

        [Fact]
        public async Task TestConflict()
        {
            await Db.ActionQueue.DispatchAsync(() => Db.ConflictResolver = new TheirsWins());
            var doc = await SetupConflict();
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc.Save();
                doc["name"].Should().Be("Scotty", "because the 'theirs' version should win");
            });

            doc = await Db.ActionQueue.DispatchAsync(() => Db["doc2"]);
            var properties = await doc.ActionQueue.DispatchAsync(() =>
            {
                doc.ConflictResolver = new MergeThenTheirsWins();
                doc["type"] = "profile";
                doc["name"] = "Scott";
                doc.Save();

                return doc.Properties;
            });

            properties["type"] = "bio";
            properties["gender"] = "male";
            SaveProperties(properties, doc.Id);

            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc["type"] = "biography";
                doc["age"] = 31;
                doc.Save();
                doc["age"].Should().Be(31, "because 'age' was changed by 'mine' and not 'theirs'");
                doc["type"].Should().Be("bio", "because 'type' was changed by 'mine' and 'theirs' so 'theirs' should win");
                doc["gender"].Should().Be("male", "because 'gender' was changed by 'theirs' but not 'mine'");
                doc["name"].Should().Be("Scott", "because 'name' was unchanged");
            });
        }

        [Fact]
        public async Task TestConflictResolverGivesUp()
        {
            await Db.ActionQueue.DispatchAsync(() => Db.ConflictResolver = new GiveUp());
            var doc = await SetupConflict();
            await doc.ActionQueue.DispatchAsync(() =>
            {
                var ex = doc.Invoking(d => d.Save()).ShouldThrow<LiteCoreException>().Which.Error.Should().Be(new C4Error(LiteCoreError.Conflict), "because the conflict resolver gave up");
                doc.ToConcrete().HasChanges.Should().BeTrue("because the document wasn't saved");
            });
        }

        [Fact]
        public async Task TestDeletionConflict()
        {
            await Db.ActionQueue.DispatchAsync(() => Db.ConflictResolver = new DoNotResolve());
            var doc = await SetupConflict();
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc.Delete();
                doc.Exists.Should().BeTrue("because there was a conflict in place of the deletion");
                doc.IsDeleted.Should().BeFalse("because there was a conflict in place of the deletion");
                doc["name"].Should().Be("Scotty", "because that was the pre-deletion value");
            });
        }

        [Fact]
        public async Task TestConflictMineIsDeeper()
        {
            await Db.ActionQueue.DispatchAsync(() => Db.ConflictResolver = null);
            var doc = await SetupConflict();
            await doc.ActionQueue.DispatchAsync(() =>
            {
                doc.Save();
                doc["name"].Should().Be("Scott Pilgrim", "because the current in memory document has a longer history");
            });
        }

        [Fact]
        public async Task TestConflictTheirsIsDeeper()
        {
            await Db.ActionQueue.DispatchAsync(() => Db.ConflictResolver = null);
            var doc = await SetupConflict();

            // Add another revision to the conflict, so it'll have a higher generation
            await doc.ActionQueue.DispatchAsync(() =>
            {
                var properties = doc.Properties;
                properties["name"] = "Scott of the Sahara";
                SaveProperties(properties, doc.Id);
                doc.Save();

                doc["name"].Should().Be("Scott of the Sahara", "because the conflict has a longer history");
            });
        }

        [Fact]
        public async Task TestBlob()
        {
            var content = Encoding.UTF8.GetBytes("12345");
            var data = BlobFactory.Create("text/plain", content);
            await _docQueue.DispatchAsync(() =>
            {
                _doc["data"] = data;
                _doc["name"] = "Jim";
                _doc.Save();
            });

            using(var otherDb = DatabaseFactory.Create(Db)) {
               
                var doc1 = otherDb.ActionQueue.DispatchSync(() => otherDb["doc1"]);
                var doc1Queue = doc1.ActionQueue;
                await doc1Queue.DispatchAsync(() =>
                {
                    doc1["name"].Should().Be("Jim", "because the document should be persistent after save");
                    doc1["data"].Should().BeAssignableTo<IBlob>("because otherwise the data did not save correctly");
                    data = doc1.GetBlob("data");
                });

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
        public async Task TestEmptyBlob()
        {
            var content = new byte[0];
            var data = BlobFactory.Create("text/plain", content);
            await _docQueue.DispatchAsync(() =>
            {
                _doc["data"] = data;
                _doc.Save();
            });

            using(var otherDb = DatabaseFactory.Create(Db)) {
                var doc1 = await otherDb.ActionQueue.DispatchAsync(() => otherDb["doc1"]);
                await doc1.ActionQueue.DispatchAsync(() =>
                {
                    doc1["data"].Should().BeAssignableTo<IBlob>("because otherwise the data did not save correctly");
                    data = doc1.GetBlob("data");
                    data.Length.Should().Be(0, "because the data is 5 bytes long");
                    data.Content.Should().Equal(content, "because the data should have been retrieved correctly");
                    var contentStream = data.ContentStream;
                    var buffer = new byte[10];
                    var bytesRead = contentStream.Read(buffer, 0, 10);
                    contentStream.Dispose();
                    bytesRead.Should().Be(0, "because the data is 5 bytes long");
                });
            }
        }

        [Fact]
        public async Task TestBlobWithStream()
        {
            var content = new byte[0];
            Stream contentStream = new MemoryStream(content);
            var data = BlobFactory.Create("text/plain", contentStream);
            await _docQueue.DispatchAsync(() =>
            {
                _doc["data"] = data;
                _doc.Save();
            });

            using(var otherDb = DatabaseFactory.Create(Db)) {
                var doc1 = await otherDb.ActionQueue.DispatchAsync(() => otherDb["doc1"]);
                await doc1.ActionQueue.DispatchAsync(() =>
                {
                    doc1["data"].Should().BeAssignableTo<IBlob>("because otherwise the data did not save correctly");
                    data = doc1.GetBlob("data");
                });

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
        public async Task TestMultipleBlobRead()
        {
            var content = Encoding.UTF8.GetBytes("12345");
            var data = BlobFactory.Create("text/plain", content);
            data = await _docQueue.DispatchAsync(() =>
            {
                _doc["data"] = data;
                return _doc.GetBlob("data");
            });

            for(int i = 0; i < 5; i++) {
                data.Content.Should().Equal(content, "because otherwise incorrect data was read");
                using(var contentStream = data.ContentStream) {
                    var buffer = new byte[10];
                    var bytesRead = contentStream.Read(buffer, 0, 10);
                    bytesRead.Should().Be(5, "because the data has 5 bytes");
                }
            }

            await _docQueue.DispatchAsync(() => _doc.Save());
            using(var otherDb = DatabaseFactory.Create(Db)) {
                var doc1 = await otherDb.ActionQueue.DispatchAsync(() => otherDb["doc1"]);
                data = await doc1.ActionQueue.DispatchAsync(() =>
                {
                    doc1["data"].Should().BeAssignableTo<IBlob>("because otherwise the data did not save correctly");
                    return doc1.GetBlob("data");
                }); 

                for(int i = 0; i < 5; i++) {
                    data.Content.Should().Equal(content, "because otherwise incorrect data was read");
                    using(var contentStream = data.ContentStream) {
                        var buffer = new byte[10];
                        var bytesRead = contentStream.Read(buffer, 0, 10);
                        bytesRead.Should().Be(5, "because the data has 5 bytes");
                    }
                }
            }
        }

        [Fact]
        public async Task TestReadExistingBlob()
        {
            var content = Encoding.UTF8.GetBytes("12345");
            var data = BlobFactory.Create("text/plain", content);
            await _docQueue.DispatchAsync(() => { 
                _doc["data"] = data;
                _doc["name"] = "Jim";
                _doc.Save();
            });

            ReopenDB();

            _doc = await Db.ActionQueue.DispatchAsync(() => Db["doc1"]);
            _docQueue = _doc.ActionQueue;
            await _docQueue.DispatchAsync(() =>
            {
                _doc["data"].As<IBlob>().Content.Should().Equal(content, "because the data should have been retrieved correctly");
            });

            ReopenDB();

            _doc = await Db.ActionQueue.DispatchAsync(() => Db["doc1"]);
            _docQueue = _doc.ActionQueue;
            await _docQueue.DispatchAsync(() =>
            {
                _doc["foo"] = "bar";
                _doc.Save();
                _doc["data"].As<IBlob>().Content.Should().Equal(content, "because the data should have been retrieved correctly");
            });
        }

        private async Task<IDocument> SetupConflict()
        {
            var doc = await Db.ActionQueue.DispatchAsync(() => Db["doc1"]);
            var properties = await doc.ActionQueue.DispatchAsync(() =>
            {
                doc["type"] = "profile";
                doc["name"] = "Scott";
                doc.Save();
                return doc.Properties;
            });

            properties["name"] = "Scotty";
            SaveProperties(properties, doc.Id);

            await doc.ActionQueue.DispatchAsync(() => doc["name"] = "Scott Pilgrim");
            return doc;
        }

        private unsafe bool SaveProperties(IDictionary<string, object> props, string docID)
        {
            var ok = Db.ActionQueue.DispatchSync(() =>
            {
                return Db.InBatch(() =>
                {
                    var tricky = (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(Db.ToConcrete().c4db, docID, true, err));
                    var put = new C4DocPutRequest {
                        docID = tricky->docID,
                        history = &tricky->revID,
                        historyCount = 1,
                        save = true
                    };

                    var body = Db.ToConcrete().JsonSerializer.Serialize(props);
                    put.body = body;

                    var newDoc = (C4Document*)LiteCoreBridge.Check(err =>
                   {
                       var localPut = put;
                       var retVal = Native.c4doc_put(Db.ToConcrete().c4db, &localPut, null, err);
                       Native.FLSliceResult_Free(body);
                       return retVal;
                   });

                    return true;
                });
            });

            ok.Should().BeTrue("beacuse otherwise the batch failed in SaveProperties");
            return ok;
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing) {
                _doc.ActionQueue.DispatchSync(() => _doc.Revert());
            }

            base.Dispose(disposing);
        }
    }

    internal class TheirsWins : IConflictResolver
    {
        public IDictionary<string, object> Resolve(IReadOnlyDictionary<string, object> mine, IReadOnlyDictionary<string, object> theirs, IReadOnlyDictionary<string, object> baseProps)
        {
            return theirs.ToDictionary(k => k.Key, v => v.Value);
        }
    }

    internal class MergeThenTheirsWins : IConflictResolver
    {
        public IDictionary<string, object> Resolve(IReadOnlyDictionary<string, object> mine, IReadOnlyDictionary<string, object> theirs, IReadOnlyDictionary<string, object> baseProps)
        {
            var resolved = baseProps.ToDictionary(k => k.Key, v => v.Value);
            var changed = new HashSet<string>();
            foreach(var pair in theirs) {
                resolved[pair.Key] = pair.Value;
                changed.Add(pair.Key);
            }

            foreach(var pair in mine) {
                if(!changed.Contains(pair.Key)) {
                    resolved[pair.Key] = pair.Value;
                }
            }

            return resolved;
        }
    }

    internal class GiveUp : IConflictResolver
    {
        public IDictionary<string, object> Resolve(IReadOnlyDictionary<string, object> mine, IReadOnlyDictionary<string, object> theirs, IReadOnlyDictionary<string, object> baseProps)
        {
            return null;
        }
    }

    internal class DoNotResolve : IConflictResolver
    {
        public IDictionary<string, object> Resolve(IReadOnlyDictionary<string, object> mine, IReadOnlyDictionary<string, object> theirs, IReadOnlyDictionary<string, object> baseProps)
        {
            throw new NotImplementedException();
        }
    }
}

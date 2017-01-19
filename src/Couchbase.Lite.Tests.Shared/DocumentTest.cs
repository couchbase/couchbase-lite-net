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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;
using FluentAssertions;
using LiteCore;
using Xunit;

namespace Test
{
    public class DocumentTest : TestCase
    {
        [Fact]
        public void TestNewDoc()
        {
            var doc = Db.GetDocument();
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

            doc.Save().Should().BeTrue("because the save should succeed");
            doc.Exists.Should().BeTrue("because the document was saved");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.Properties.Should().BeEmpty("because no properties were added");
        }

        [Fact]
        public void TestNewDocWithID()
        {
            var doc = Db.GetDocument("doc1");
            Db.GetDocument("doc1").Should().BeSameAs(doc, "because the document should be cached");
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

            doc.Save().Should().BeTrue("because the save should succeed");
            doc.Exists.Should().BeTrue("because the document was saved");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.Properties.Should().BeEmpty("because no properties were added");
            Db.GetDocument("doc1").Should().BeSameAs(doc, "because the document should be cached");
        }

        [Fact]
        public void TestPropertyAccessors()
        {
            var doc = Db.GetDocument("doc1");
            var date = DateTimeOffset.Now;
            doc.Set("bool", true)
                .Set("double", 1.1)
                .Set("float", 1.2f)
                .Set("integer", 2L)
                .Set("string", "str")
                //.Set("dict", new Dictionary<string, object> { ["foo"] = "bar" })
                .Set("array", new[] { "1", "2" })
                .Set("date", date)
                .Save().Should().BeTrue("because the save should succeed");

            doc.GetBoolean("bool").Should().BeTrue("because that is the bool that was saved");
            doc.GetDouble("double").Should().BeApproximately(1.1, Double.Epsilon, "because that is the double that was saved");
            doc.GetFloat("float").Should().BeApproximately(1.2f, Single.Epsilon, "because that is the float that was saved");
            doc.GetLong("integer").Should().Be(2L, "because that is the integer that was saved");

            doc.GetString("string").Should().Be("str", "because that is the string that was saved");
            //doc.Get("dict").ShouldBeEquivalentTo(new Dictionary<string, object> { ["foo"] = "bar" }, "because that is the dict that was saved");
            doc.Get("array").ShouldBeEquivalentTo(new[] { "1", "2" }, "because that is the array that was saved");

            doc.GetDate("date").Should().Be(date, "because that is the date that was saved");

            // Get the doc from another database
            using(var otherDB = new Database(Db)) {
                var doc1 = otherDB.GetDocument("doc1");
                doc1.GetBoolean("bool").Should().BeTrue("because that is the bool that was saved");
                doc1.GetDouble("double").Should().BeApproximately(1.1, Double.Epsilon, "because that is the double that was saved");
                doc1.GetFloat("float").Should().BeApproximately(1.2f, Single.Epsilon, "because that is the float that was saved");
                doc1.GetLong("integer").Should().Be(2L, "because that is the integer that was saved");

                doc1.GetString("string").Should().Be("str", "because that is the string that was saved");
                //doc1.Get("dict").ShouldBeEquivalentTo(new Dictionary<string, object> { ["foo"] = "bar" }, "because that is the dict that was saved");
                doc1.Get("array").ShouldBeEquivalentTo(new[] { "1", "2" }, "because that is the array that was saved");

                doc1.GetDate("date").Should().Be(date, "because that is the date that was saved");
            }
        }

        [Fact]
        public void TestProperties()
        {
            var doc = Db["doc1"];
            doc["type"] = "demo";
            doc["weight"] = 12.5;
            doc["tags"] = new[] { "useless", "emergency" };

            doc["type"].Should().Be("demo", "because that is the type that was entered");
            doc["weight"].As<double>().Should().BeApproximately(12.5, Double.Epsilon, "beacuse that is the weight that was entered");
            doc.Properties.Should().Contain("type", "demo").And.Contain("weight", 12.5, "because those simple values were added");
            doc.Properties["tags"].As<IEnumerable<string>>().Should()
                .ContainInOrder(new[] { "useless", "emergency" }, "because those were the tags that were added");
        }

        [Fact]
        public void TestDelete()
        {
            var doc = Db["doc1"];
            doc["type"] = "Profile";
            doc["name"] = "Scott";
            doc.Exists.Should().BeFalse("because the document has not been saved yet");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

            // Delete before save:
            doc.Delete().Should().BeFalse("because deleting a non-existent document is invalid");
            doc["type"].Should().Be("Profile", "because the doc should still exist");
            doc["name"].Should().Be("Scott", "because the doc should still exist");

            // Save:
            doc.Save().Should().BeTrue("because the save should succeed");
            doc.Exists.Should().BeTrue("because the document was saved");
            doc.IsDeleted.Should().BeFalse("beacuse the document is still not deleted");

            // Delete:
            doc.Delete().Should().BeTrue("because the delete should succeed now"); ;
            doc.Exists.Should().BeTrue("because the document still exists in terms of the DB");
            doc.IsDeleted.Should().BeTrue("because now the document is deleted");
            doc.Properties.Should().BeEmpty("because a deleted document has no properties");
        }

        [Fact]
        public void TestPurge()
        {
            var doc = Db["doc1"];
            doc["type"] = "Profile";
            doc["name"] = "Scott";
            doc.Exists.Should().BeFalse("because the document has not been saved yet");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

            // Purge before save:
            doc.Purge().Should().BeFalse("because deleting a non-existent document is invalid");
            doc["type"].Should().Be("Profile", "because the doc should still exist");
            doc["name"].Should().Be("Scott", "because the doc should still exist");

            // Save:
            doc.Save().Should().BeTrue("because the save should succeed");
            doc.Exists.Should().BeTrue("because the document was saved");
            doc.IsDeleted.Should().BeFalse("beacuse the document is still not deleted");

            // Purge:
            doc.Purge().Should().BeTrue("because the purge should succeed now");
            doc.Exists.Should().BeFalse("because the document was blown away");
            doc.IsDeleted.Should().BeFalse("because the document does not exist");
            doc.Properties.Should().BeEmpty("because a purged document has no properties");
        }

        [Fact]
        public void TestRevert()
        {
            var doc = Db["doc1"];
            doc["type"] = "Profile";
            doc["name"] = "Scott";

            // Reset before save:
            doc.Revert();
            doc["type"].Should().BeNull("because the document was reset");
            doc["name"].Should().BeNull("because the document was reset");

            // Save:
            doc["type"] = "Profile";
            doc["name"] = "Scott";
            doc.Save().Should().BeTrue("because the save should succeed");
            doc["type"].Should().Be("Profile", "because the save completed");
            doc["name"].Should().Be("Scott", "because the save completed");

            // Make some changes:
            doc["type"] = "user";
            doc["name"] = "Scottie";

            // Reset:
            doc.Revert();
            doc["type"].Should().Be("Profile", "because the document was reset");
            doc["name"].Should().Be("Scott", "because the document was reset");
        }

        [Fact]
        public void TestInvalidProperties()
        {
            var doc = Db["doc1"];
            doc.Invoking(x => x.Set("dict", new Dictionary<string, object> { ["foo"] = "bar" })).ShouldThrow<ArgumentException>("because storing dictionaries directly is not allowed");

            var fancyClass = new Action(() => Debug.WriteLine("OUT!"));
            doc.Invoking(x => x.Set("action", fancyClass)).ShouldThrow<ArgumentException>("because storing dictionaries directly is not allowed");
        }
    }
}

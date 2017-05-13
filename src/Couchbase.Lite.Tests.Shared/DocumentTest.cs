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
#if !WINDOWS_UWP
        public DocumentTest(ITestOutputHelper output) : base(output)
        {
            
        }
#endif

        [Fact]
        public void TestCreateDoc()
        {
            var doc1a = new Document();
            doc1a.Id.Should().NotBeNullOrEmpty("because every document should have an ID");
            doc1a.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc1a.ToDictionary().Should().BeEmpty("because the document has no properties");

            var doc1b = SaveDocument(doc1a);
            doc1b.As<object>()
                .Should()
                .NotBeSameAs(doc1a, "because each call to GetDocument should return a unique instance");
            doc1b.Id.Should().Be(doc1a.Id, "because the two document objects should have the same ID");
        }

        [Fact]
        public void TestCreateDocWithID()
        {
            var doc1a = new Document("doc1");
            doc1a.Id.Should().Be("doc1", "because that was the ID it was given");
            doc1a.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc1a.ToDictionary().Should().BeEmpty("because the document has no properties");

            var doc1b = SaveDocument(doc1a);
            doc1b.As<object>()
                .Should()
                .NotBeSameAs(doc1a, "because each call to GetDocument should return a unique instance");
            doc1b.Id.Should().Be(doc1a.Id, "because the two document objects should have the same ID");
        }

        [Fact]
        public void TestCreateDocWithEmptyStringID()
        {
            var doc1a = new Document("");
            doc1a.Id.Should().BeEmpty("because that was the ID it was given");
            doc1a.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc1a.ToDictionary().Should().BeEmpty("because the document has no properties");

            Db.Invoking(d => d.Save(doc1a))
                .ShouldThrow<LiteCoreException>()
                .Which.Error.Should()
                .Match<C4Error>(e => e.code == (int) LiteCoreError.BadDocID &&
                                     e.domain == C4ErrorDomain.LiteCoreDomain);
        }

        [Fact]
        public void TestCreateDocWithNullID()
        {
            var doc1a = new Document(default(string));
            doc1a.Id.Should().NotBeNullOrEmpty("because every document should have an ID");
            doc1a.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc1a.ToDictionary().Should().BeEmpty("because the document has no properties");

            var doc1b = SaveDocument(doc1a);
            doc1b.As<object>()
                .Should()
                .NotBeSameAs(doc1a, "because each call to GetDocument should return a unique instance");
            doc1b.Id.Should().Be(doc1a.Id, "because the two document objects should have the same ID");
        }

        [Fact]
        public void TestCreateDocWithDict()
        {
            var dict = new Dictionary<string, object> {
                ["name"] = "Scott Tiger",
                ["age"] = 30,
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Main Street.",
                    ["city"] = "Mountain View",
                    ["state"] = "CA"
                },
                ["phones"] = new List<object> {"650-123-0001", "650-123-0002"}
            };

            var doc1a = new Document(dict);
            doc1a.Id.Should().NotBeNullOrEmpty("because every document should have an ID");
            doc1a.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc1a.ToDictionary().ShouldBeEquivalentTo(dict, "because the document was given properties");

            var doc1b = SaveDocument(doc1a);
            doc1b.As<object>()
                .Should()
                .NotBeSameAs(doc1a, "because each call to GetDocument should return a unique instance");
            doc1b.Id.Should().Be(doc1a.Id, "because the two document objects should have the same ID");
            doc1b.ToDictionary().ShouldBeEquivalentTo(dict, "because the document was saved with properties");
        }

        [Fact]
        public void TestCreateDocWithIDAndDict()
        {
            var dict = new Dictionary<string, object> {
                ["name"] = "Scott Tiger",
                ["age"] = 30,
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Main Street.",
                    ["city"] = "Mountain View",
                    ["state"] = "CA"
                },
                ["phones"] = new List<object> { "650-123-0001", "650-123-0002" }
            };

            var doc1a = new Document("doc1", dict);
            doc1a.Id.Should().Be("doc1", "because that was the ID it was given");
            doc1a.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc1a.ToDictionary().ShouldBeEquivalentTo(dict, "because the document was given properties");

            var doc1b = SaveDocument(doc1a);
            doc1b.As<object>()
                .Should()
                .NotBeSameAs(doc1a, "because each call to GetDocument should return a unique instance");
            doc1b.Id.Should().Be(doc1a.Id, "because the two document objects should have the same ID");
            doc1b.ToDictionary().ShouldBeEquivalentTo(dict, "because the document was saved with properties");
        }

        [Fact]
        public void TestSetDictionaryContent()
        {
            var dict = new Dictionary<string, object> {
                ["name"] = "Scott Tiger",
                ["age"] = 30,
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Main Street.",
                    ["city"] = "Mountain View",
                    ["state"] = "CA"
                },
                ["phones"] = new List<object> { "650-123-0001", "650-123-0002" }
            };

            var doc = new Document("doc1");
            doc.Set(dict);
            doc.ToDictionary().ShouldBeEquivalentTo(dict, "because that is what was just set");

            var nuDict = new Dictionary<string, object> {
                ["name"] = "Danial Tiger",
                ["age"] = 32,
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "2 Main Street.",
                    ["city"] = "Palo Alto",
                    ["state"] = "CA"
                },
                ["phones"] = new List<object> { "650-234-0001", "650-234-0002" }
            };

            doc.Set(nuDict);
            doc.ToDictionary().ShouldBeEquivalentTo(nuDict, "because that is what was just set");

            doc = SaveDocument(doc);
            doc.ToDictionary().ShouldBeEquivalentTo(nuDict, "because that is what was just saved");
        }

        [Fact]
        public void TestGetValueFromNewEmptyDoc()
        {
            var doc = new Document("doc1");
            SaveDocument(doc, d =>
            {
                d.GetInt("key").Should().Be(0, "because no integer exists for this key");
                d.GetDouble("key").Should().Be(0.0, "because no double exists for this key");
                d.GetBoolean("key").Should().BeFalse("because no boolean exists for this key");
                d.GetBlob("key").Should().BeNull("because no blob exists for this key");
                d.GetDate("key").Should().Be(DateTimeOffset.MinValue, "because no date exists for this key");
                d.GetObject("key").Should().BeNull("because no object exists for this key");
                d.GetString("key").Should().BeNull("because no string exists for this key");
                d.GetDictionary("key").Should().BeNull("because no subdocument exists for this key");
                d.GetArray("key").Should().BeNull("because no array exists for this key");
                d.ToDictionary().Should().BeEmpty("because this document has no properties");
            });
        }

        [Fact]
        public void TestGetValueFromExistingEmptyDoc()
        {
            var doc = new Document("doc1");
            doc = SaveDocument(doc);

            doc.GetInt("key").Should().Be(0, "because no integer exists for this key");
            doc.GetDouble("key").Should().Be(0.0, "because no double exists for this key");
            doc.GetBoolean("key").Should().BeFalse("because no boolean exists for this key");
            doc.GetBlob("key").Should().BeNull("because no blob exists for this key");
            doc.GetDate("key").Should().Be(DateTimeOffset.MinValue, "because no date exists for this key");
            doc.GetObject("key").Should().BeNull("because no object exists for this key");
            doc.GetString("key").Should().BeNull("because no string exists for this key");
            doc.GetDictionary("key").Should().BeNull("because no subdocument exists for this key");
            doc.GetArray("key").Should().BeNull("because no array exists for this key");
            doc.ToDictionary().Should().BeEmpty("because this document has no properties");
        }

        [Fact]
        public void TestSaveThenGetFromAnotherDB()
        {
            var doc1a = new Document("doc1");
            doc1a.Set("name", "Scott Tiger");

            SaveDocument(doc1a);

            using (var anotherDb = new Database(Db)) {
                var doc1b = anotherDb.GetDocument("doc1");
                doc1b.As<object>().Should().NotBeSameAs(doc1a, "because unique instances should be returned");
                doc1a.Id.Should().Be(doc1b.Id, "because object for the same document should have matching IDs");
                doc1a.ToDictionary().ShouldBeEquivalentTo(doc1b.ToDictionary(), "because the contents should match");
            }
        }

        [Fact]
        public void TestNoCacheNoLive()
        {
            var doc1a = new Document("doc1");
            doc1a.Set("name", "Scott Tiger");

            SaveDocument(doc1a);

            var doc1b = Db.GetDocument("doc1");
            var doc1c = Db.GetDocument("doc1");

            using (var anotherDb = new Database(Db)) {
                var doc1d = anotherDb.GetDocument("doc1");

                doc1a.As<object>().Should().NotBeSameAs(doc1b, "because unique instances should be returned");
                doc1a.As<object>().Should().NotBeSameAs(doc1c, "because unique instances should be returned");
                doc1a.As<object>().Should().NotBeSameAs(doc1d, "because unique instances should be returned");
                doc1b.As<object>().Should().NotBeSameAs(doc1c, "because unique instances should be returned");
                doc1b.As<object>().Should().NotBeSameAs(doc1d, "because unique instances should be returned");
                doc1c.As<object>().Should().NotBeSameAs(doc1d, "because unique instances should be returned");

                doc1a.ToDictionary().ShouldBeEquivalentTo(doc1b.ToDictionary(), "because the contents should match");
                doc1a.ToDictionary().ShouldBeEquivalentTo(doc1c.ToDictionary(), "because the contents should match");
                doc1a.ToDictionary().ShouldBeEquivalentTo(doc1d.ToDictionary(), "because the contents should match");

                doc1b.Set("name", "Daniel Tiger");
                SaveDocument(doc1b);

                doc1b.Equals(doc1a).Should().BeFalse("because the contents should not match anymore");
                doc1b.Equals(doc1c).Should().BeFalse("because the contents should not match anymore");
                doc1b.Equals(doc1d).Should().BeFalse("because the contents should not match anymore");
            }
        }

        [Fact]
        public void TestSetString()
        {
            var doc = new Document("doc1");
            doc.Set("string1", "");
            doc.Set("string2", "string");

            SaveDocument(doc, d =>
            {
                d.GetString("string1").Should().Be("", "because that is the value of the first revision of string1");
                d.GetString("string2")
                    .Should()
                    .Be("string", "because that is the value of the first revision of string2");
            });

            doc.Set("string2", "");
            doc.Set("string1", "string");

            SaveDocument(doc, d =>
            {
                d.GetString("string2").Should().Be("", "because that is the value of the second revision of string2");
                d.GetString("string1")
                    .Should()
                    .Be("string", "because that is the value of the second revision of string1");
            });
        }

        [Fact]
        public void TestGetString()
        {
            var doc = new Document("doc1");
            PopulateData(doc);
            SaveDocument(doc, d =>
            {
                d.GetString("true").Should().BeNull("because there is no string in 'true'");
                d.GetString("false").Should().BeNull("because there is no string in 'false'");
                d.GetString("string").Should().Be("string", "because there is a string in 'string'");
                d.GetString("zero").Should().BeNull("because there is no string in 'zero'");
                d.GetString("one").Should().BeNull("because there is no string in 'one'");
                d.GetString("minus_one").Should().BeNull("because there is no string in 'minus_one'");
                d.GetString("one_dot_one").Should().BeNull("because there is no string in 'one_dot_one'");
                d.GetString("date").Should().Be(d.GetDate("date").ToString("o"), "because date is convertible to string");
                d.GetString("dict").Should().BeNull("because there is no string in 'subdoc'");
                d.GetString("array").Should().BeNull("because there is no string in 'array'");
                d.GetString("blob").Should().BeNull("because there is no string in 'blob'");
                d.GetString("non_existing_key").Should().BeNull("because that key has no value");
            });
        }

        [Fact]
        public void TestSetNumber()
        {
            var doc = new Document("doc1");
            doc.Set("number1", 1);
            doc.Set("number2", 0);
            doc.Set("number3", -1);
            doc.Set("number4", 1.1);

            SaveDocument(doc, d =>
            {
                d.GetInt("number1").Should().Be(1, "because that is the value of the first revision of number1");
                d.GetInt("number2").Should().Be(0, "because that is the value of the first revision of number2");
                d.GetInt("number3").Should().Be(-1, "because that is the value of the first revision of number3");
                d.GetDouble("number4").Should().Be(1.1, "because that is the value of the first revision of number4");
            });

            doc.Set("number1", 0);
            doc.Set("number2", 1);
            doc.Set("number3", 1.1);
            doc.Set("number4", -1);

            SaveDocument(doc, d =>
            {
                d.GetInt("number1").Should().Be(0, "because that is the value of the second revision of number1");
                d.GetInt("number2").Should().Be(1, "because that is the value of the second revision of number2");
                d.GetDouble("number3").Should().Be(1.1, "because that is the value of the second revision of number3");
                d.GetInt("number4").Should().Be(-1, "because that is the value of the second revision of number4");
            });
        }

        [Fact]
        public void TestGetInteger()
        {
            var doc = new Document("doc1");
            PopulateData(doc);
            SaveDocument(doc, d =>
            {
                d.GetInt("true").Should().Be(1, "because a true bool value will be coalesced to 1");
                d.GetInt("false").Should().Be(0, "because a false bool value will be coalesced to 0");
                d.GetInt("string").Should().Be(0, "because that is the default value");
                d.GetInt("zero").Should().Be(0, "because zero was stored in this key");
                d.GetInt("one").Should().Be(1, "because one was stored in this key");
                d.GetInt("minus_one").Should().Be(-1, "because -1 was stored in this key");
                d.GetInt("one_dot_one").Should().Be(1, "because 1.1 gets truncated to 1");
                d.GetInt("date").Should().Be(0, "because that is the default value");
                d.GetInt("dict").Should().Be(0, "because that is the default value");
                d.GetInt("array").Should().Be(0, "because that is the default value");
                d.GetInt("blob").Should().Be(0, "because that is the default value");
                d.GetInt("non_existing_key").Should().Be(0, "because that key has no value");
            });
        }

        [Fact]
        public void TestGetDouble()
        {
            var doc = new Document("doc1");
            PopulateData(doc);
            SaveDocument(doc, d =>
            {
                d.GetDouble("true").Should().Be(1.0, "because a true bool value will be coalesced to 1.0");
                d.GetDouble("false").Should().Be(0.0, "because a false bool value will be coalesced to 0.0");
                d.GetDouble("string").Should().Be(0.0, "because that is the default value");
                d.GetDouble("zero").Should().Be(0.0, "because zero was stored in this key");
                d.GetDouble("one").Should().Be(1.0, "because one was stored in this key");
                d.GetDouble("minus_one").Should().Be(-1.0, "because -1 was stored in this key");
                d.GetDouble("one_dot_one").Should().Be(1.1, "because 1.1 was stored in this key");
                d.GetDouble("date").Should().Be(0.0, "because that is the default value");
                d.GetDouble("dict").Should().Be(0.0, "because that is the default value");
                d.GetDouble("array").Should().Be(0.0, "because that is the default value");
                d.GetDouble("blob").Should().Be(0.0, "because that is the default value");
                d.GetDouble("non_existing_key").Should().Be(0.0, "because that key has no value");
            });
        }

        [Fact]
        public void TestSetGetMinMaxNumbers()
        {
            var doc = new Document("doc1");
            doc.Set("min_int", Int64.MinValue);
            doc.Set("max_int", Int64.MaxValue);
            doc.Set("min_double", Double.MinValue);
            doc.Set("max_double", Double.MaxValue);

            SaveDocument(doc, d =>
            {
                d.GetLong("min_int").Should().Be(Int64.MinValue, "because that is what was stored");
                d.GetLong("max_int").Should().Be(Int64.MaxValue, "because that is what was stored");
                d.GetDouble("min_double").Should().Be(Double.MinValue, "because that is what was stored");
                d.GetDouble("max_double").Should().Be(Double.MaxValue, "because that is what was stored");
            });
        }

        [Fact]
        public void TestSetBoolean()
        {
            var doc = new Document("doc1");
            doc.Set("boolean1", true);
            doc.Set("boolean2", false);

            SaveDocument(doc, d =>
            {
                d.GetBoolean("boolean1").Should().Be(true, "because that is the value of the first revision of boolean1");
                d.GetBoolean("boolean2").Should().Be(false, "because that is the value of the first revision of boolean2");
            });

            doc.Set("boolean1", false);
            doc.Set("boolean2", true);

            SaveDocument(doc, d =>
            {
                d.GetBoolean("boolean1").Should().Be(false, "because that is the value of the second revision of boolean1");
                d.GetBoolean("boolean2").Should().Be(true, "because that is the value of the second revision of boolean2");
            });
        }

        [Fact]
        public void TestGetBoolean()
        {
            var doc = new Document("doc1");
            PopulateData(doc);
            SaveDocument(doc, d =>
            {
                d.GetBoolean("true").Should().Be(true, "because true was stored");
                d.GetBoolean("false").Should().Be(false, "because false was stored");
                d.GetBoolean("string").Should().Be(true, "because any non-zero object will be true");
                d.GetBoolean("zero").Should().Be(false, "because zero will coalesce to false");
                d.GetBoolean("one").Should().Be(true, "because any non-zero object will be true");
                d.GetBoolean("minus_one").Should().Be(true, "because any non-zero object will be true");
                d.GetBoolean("one_dot_one").Should().Be(true, "because any non-zero object will be true");
                d.GetBoolean("date").Should().Be(true, "because any non-zero object will be true");
                d.GetBoolean("dict").Should().Be(true, "because any non-zero object will be true");
                d.GetBoolean("array").Should().Be(true, "because any non-zero object will be true");
                d.GetBoolean("blob").Should().Be(true, "because any non-zero object will be true");
                d.GetBoolean("non_existing_key").Should().Be(false, "because that key has no value");
            });
        }

        [Fact]
        public void TestSetDate()
        {
            var doc = new Document("doc1");
            var date = DateTimeOffset.Now;
            var dateStr = date.ToString("o");
            doc.Set("date", dateStr);

            SaveDocument(doc, d =>
            {
                d.GetObject("date").Should().Be(dateStr, "because that is what was stored");
                d.GetString("date").Should().Be(dateStr, "because a string was stored");
                d.GetDate("date").Should().Be(date, "because the string is convertible to a date");
            });

            var nuDate = date + TimeSpan.FromSeconds(60);
            var nuDateStr = nuDate.ToString("o");
            doc.Set("date", nuDate);
            
            SaveDocument(doc, d =>
            {
                d.GetDate("date").Should().Be(nuDate, "because that is what was stored the second time");
                d.GetString("date").Should().Be(nuDateStr, "because the date is convertible to a string");
            });
        }

        [Fact]
        public void TestGetDate()
        {
            var doc = new Document("doc1");
            PopulateData(doc);
            SaveDocument(doc, d =>
            {
                d.GetDate("true").Should().Be(DateTimeOffset.MinValue, "because that is the default");
                d.GetDate("false").Should().Be(DateTimeOffset.MinValue, "because that is the default");
                d.GetDate("string").Should().Be(DateTimeOffset.MinValue, "because that is the default");
                d.GetDate("zero").Should().Be(DateTimeOffset.MinValue, "because that is the default");
                d.GetDate("one").Should().Be(DateTimeOffset.MinValue, "because that is the default");
                d.GetDate("minus_one").Should().Be(DateTimeOffset.MinValue, "because that is the default");
                d.GetDate("one_dot_one").Should().Be(DateTimeOffset.MinValue, "because that is the default");
                d.GetDate("date").ToString("o").Should().Be(d.GetString("date"), "because the date and its string should match");
                d.GetDate("dict").Should().Be(DateTimeOffset.MinValue, "because that is the default");
                d.GetDate("array").Should().Be(DateTimeOffset.MinValue, "because that is the default");
                d.GetDate("blob").Should().Be(DateTimeOffset.MinValue, "because that is the default");
                d.GetDate("non_existing_key").Should().Be(DateTimeOffset.MinValue, "because that key has no value");
            });
        }

        [Fact]
        public void TestSetBlob()
        {
            var doc = new Document("doc1");
            var content = Encoding.UTF8.GetBytes("12345");
            var blob = new Blob("text/plain", content);
            doc.Set("blob", blob);

            SaveDocument(doc, d =>
            {
                d.GetObject("blob")
                    .As<Blob>()
                    .Properties.ShouldBeEquivalentTo(blob.Properties,
                        "because otherwise the blob did not store correctly");
                d.GetBlob("blob")
                    .Properties.ShouldBeEquivalentTo(blob.Properties,
                    "because otherwise the blob did not store correctly");
                d.GetBlob("blob")
                    .Content.ShouldBeEquivalentTo(blob.Content,
                        "because otherwise the blob did not store correctly");
            });

            var nuContent = Encoding.UTF8.GetBytes("1234567890");
            var nuBlob = new Blob("text/plain", nuContent);
            doc.Set("blob", nuBlob);

            SaveDocument(doc, d =>
            {
                d.GetObject("blob")
                    .As<Blob>()
                    .Properties.ShouldBeEquivalentTo(nuBlob.Properties,
                        "because otherwise the blob did not update correctly");
                d.GetBlob("blob")
                    .Properties.ShouldBeEquivalentTo(nuBlob.Properties,
                        "because otherwise the blob did not update correctly");
                d.GetBlob("blob")
                    .Content.ShouldBeEquivalentTo(nuBlob.Content,
                        "because otherwise the blob did not update correctly");
            });
        }

        [Fact]
        public void TestGetBlob()
        {
            var doc = new Document("doc1");
            PopulateData(doc);
            SaveDocument(doc, d =>
            {
                d.GetBlob("true").Should().BeNull("because that is the default");
                d.GetBlob("false").Should().BeNull("because that is the default");
                d.GetBlob("string").Should().BeNull("because that is the default");
                d.GetBlob("zero").Should().BeNull("because that is the default");
                d.GetBlob("one").Should().BeNull("because that is the default");
                d.GetBlob("minus_one").Should().BeNull("because that is the default");
                d.GetBlob("one_dot_one").Should().BeNull("because that is the default");
                d.GetBlob("date").Should().BeNull("because that is the default");
                d.GetBlob("dict").Should().BeNull("because that is the default");
                d.GetBlob("array").Should().BeNull("because that is the default");
                d.GetBlob("blob").Should().NotBeNull().And.Subject.As<Blob>()
                    .Content.ShouldBeEquivalentTo(Encoding.UTF8.GetBytes("12345"),
                        "because that is the content that was stored");
                d.GetBlob("non_existing_key").Should().BeNull("because that key has no value");
            });
        }

        [Fact]
        public void TestSetDictionary()
        {
            var doc = new Document("doc1");
            IDictionaryObject dict = new DictionaryObject();
            dict.Set("street", "1 Main street");
            doc.Set("dict", dict);

            doc.GetObject("dict").Should().Be(dict, "because that is what was stored");
            doc = SaveDocument(doc);

            doc.GetObject("dict").Should().NotBeSameAs(dict, "beacuse a new document should return a new object");
            doc.GetObject("dict")
                .Should()
                .BeSameAs(doc.GetDictionary("dict"), "because the same document should return the same thing");
            doc.GetDictionary("dict")
                .ToDictionary()
                .ShouldBeEquivalentTo(dict.ToDictionary(), "because the contents should be the same");

            dict = doc.GetDictionary("dict");
            dict.Set("city", "Mountain View");
            doc.GetObject("dict")
                .Should()
                .BeSameAs(doc.GetDictionary("dict"), "because the same document should return the same thing");
            var csharpDict = new Dictionary<string, object> {
                ["street"] = "1 Main street",
                ["city"] = "Mountain View"
            };
            doc.GetDictionary("dict")
                .ToDictionary()
                .ShouldBeEquivalentTo(csharpDict, "because otherwise the contents are incorrect");

            doc = SaveDocument(doc);
            doc.GetObject("dict").Should().NotBeSameAs(dict, "beacuse a new document should return a new object");
            doc.GetObject("dict")
                .Should()
                .BeSameAs(doc.GetDictionary("dict"), "because the same document should return the same thing");
            doc.GetDictionary("dict")
                .ToDictionary()
                .ShouldBeEquivalentTo(csharpDict, "because the contents should be the same as before");
        }

        [Fact]
        public void TestGetDictionary()
        {
            var doc = new Document("doc1");
            PopulateData(doc);
            SaveDocument(doc, d =>
            {
                d.GetDictionary("true").Should().BeNull("because that is the default");
                d.GetDictionary("false").Should().BeNull("because that is the default");
                d.GetDictionary("string").Should().BeNull("because that is the default");
                d.GetDictionary("zero").Should().BeNull("because that is the default");
                d.GetDictionary("one").Should().BeNull("because that is the default");
                d.GetDictionary("minus_one").Should().BeNull("because that is the default");
                d.GetDictionary("one_dot_one").Should().BeNull("because that is the default");
                d.GetDictionary("date").Should().BeNull("because that is the default");
                var csharpDict = new Dictionary<string, object> {
                    ["street"] = "1 Main street",
                    ["city"] = "Mountain View",
                    ["state"] = "CA"
                };
                d.GetDictionary("dict")
                    .Should()
                    .NotBeNull()
                    .And.Subject.As<IDictionaryObject>()
                    .ToDictionary()
                    .ShouldBeEquivalentTo(csharpDict, "because those are the stored contents");
                d.GetDictionary("array").Should().BeNull("because that is the default");
                d.GetDictionary("blob").Should().BeNull("because that is the default");
                d.GetDictionary("non_existing_key").Should().BeNull("because that key has no value");
            });
        }

        [Fact]
        public void TestSetArrayObject()
        {
            var doc = new Document("doc1");
            IArray array = new ArrayObject {
                "item1",
                "item2",
                "item3"
            };

            doc.Set("array", array);

            doc.GetObject("array").Should().Be(array, "because that is what was stored");
            doc.GetArray("array").As<object>().Should().Be(array, "because that is what was stored");
            doc.GetArray("array")
                .Should()
                .ContainInOrder(new[] {"item1", "item2", "item3"}, "because otherwise the contents are incorrect");

            doc = SaveDocument(doc);

            doc.GetObject("array").Should().NotBeSameAs(array, "because a new document should return a new object");
            doc.GetObject("array")
                .Should()
                .BeSameAs(doc.GetArray("array"), "because the same doc should return the same object");
            doc.GetArray("array")
                .Should()
                .ContainInOrder(new[] { "item1", "item2", "item3" }, "because otherwise the contents are incorrect");

            array = doc.GetArray("array");
            array.Add("item4");
            array.Add("item5");

            doc = SaveDocument(doc);
            doc.GetObject("array").Should().NotBeSameAs(array, "because a new document should return a new object");
            doc.GetObject("array")
                .Should()
                .BeSameAs(doc.GetArray("array"), "because the same doc should return the same object");
            doc.GetArray("array")
                .Should()
                .ContainInOrder(new[] { "item1", "item2", "item3", "item4", "item5" },
                "because otherwise the contents are incorrect");
        }

        [Fact]
        public void TestGetArray()
        {
            var doc = new Document("doc1");
            PopulateData(doc);
            SaveDocument(doc, d =>
            {
                d.GetArray("true").Should().BeNull("because that is the default");
                d.GetArray("false").Should().BeNull("because that is the default");
                d.GetArray("string").Should().BeNull("because that is the default");
                d.GetArray("zero").Should().BeNull("because that is the default");
                d.GetArray("one").Should().BeNull("because that is the default");
                d.GetArray("minus_one").Should().BeNull("because that is the default");
                d.GetArray("one_dot_one").Should().BeNull("because that is the default");
                d.GetArray("date").Should().BeNull("because that is the default");
                d.GetArray("dict").Should().BeNull("because that is the default");
                d.GetArray("array")
                    .Should()
                    .NotBeNull()
                    .And.Subject.As<IArray>()
                    .Should()
                    .ContainInOrder(new[] {"650-123-0001", "650-123-0002"}, "because that is the array that was stored");
                d.GetArray("blob").Should().BeNull("because that is the default");
                d.GetArray("non_existing_key").Should().BeNull("because that key has no value");
            });
        }

        [Fact]
        public void TestSetCSharpDictionary()
        {
            var dict = new Dictionary<string, object> {
                ["street"] = "1 Main street",
                ["city"] = "Mountain View",
                ["state"] = "CA"
            };

            var doc = new Document("doc1");
            doc.Set("address", dict);

            var address = doc.GetDictionary("address");
            address.As<object>()
                .Should().NotBeNull().And
                .BeSameAs(doc.GetObject("address"), "because the same doc should return the same object");
            address.GetString("street").Should().Be("1 Main street", "because that is the street that was stored");
            address.GetString("city").Should().Be("Mountain View", "because that is the city that was stored");
            address.GetString("state").Should().Be("CA", "because that is the state that was stored");
            address.ToDictionary().ShouldBeEquivalentTo(dict, "because the content should be the same");

            var nuDict = new Dictionary<string, object> {
                ["street"] = "1 Second street",
                ["city"] = "Palo Alto",
                ["state"] = "CA"
            };
            doc.Set("address", nuDict);

            // Make sure the old address dictionary is not affected
            address.Should().NotBeSameAs(doc.GetDictionary("address"), "because address is now detached");
            address.GetString("street").Should().Be("1 Main street", "because that is the street that was stored");
            address.GetString("city").Should().Be("Mountain View", "because that is the city that was stored");
            address.GetString("state").Should().Be("CA", "because that is the state that was stored");
            address.ToDictionary().ShouldBeEquivalentTo(dict, "because the content should be the same");
            var nuAddress = doc.GetDictionary("address");
            nuAddress.Should().NotBeSameAs(address, "beacuse they are two different entities");

            nuAddress.Set("zip", "94302");
            nuAddress.GetString("zip").Should().Be("94302", "because that was what was just stored");
            address.GetString("zip").Should().BeNull("because address should not be affected");

            doc = SaveDocument(doc);

            nuDict["zip"] = "94302";
            doc.ToDictionary()
                .ShouldBeEquivalentTo(new Dictionary<string, object> {
                    ["address"] = nuDict
                }, "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestSetCSharpList()
        {
            var array = new[] {"a", "b", "c"};
            var doc = new Document("doc1");
            doc.Set("members", array);

            var members = doc.GetArray("members");
            members.As<object>()
                .Should()
                .NotBeNull()
                .And
                .BeSameAs(doc.GetObject("members"), "because the same document should return the same object");

            members.Count.Should().Be(3, "because there are three elements inside");
            members.Should().ContainInOrder(array, "because otherwise the contents are wrong");
            members.ToArray().Should().ContainInOrder(array, "because otherwise the contents are wrong");

            var nuArray = new[] {"d", "e", "f"};
            doc.Set("members", nuArray);

            // Make sure the old members array is not affected
            members.Count.Should().Be(3, "because there are three elements inside");
            members.Should().ContainInOrder(array, "because otherwise the contents are wrong");
            members.ToArray().Should().ContainInOrder(array, "because otherwise the contents are wrong");

            var nuMembers = doc.GetArray("members");
            members.Should().NotBeSameAs(nuMembers, "because the new array should have no relation to the old");
            nuMembers.Add("g");
            nuMembers.Count.Should().Be(4, "because another element was added");
            nuMembers.GetObject(3).Should().Be("g", "because that is what was added");
            members.Count.Should().Be(3, "beacuse members still has three elements");

            doc = SaveDocument(doc);

            doc.ToDictionary()
                .ShouldBeEquivalentTo(new Dictionary<string, object> {
                    ["members"] = new[] {"d", "e", "f", "g"}
                }, "beacuse otherwise the document contents are incorrect");
        }

        [Fact]
        public void TestUpdateNestedDictionary()
        {
            var doc = new Document("doc1");
            var addresses = new DictionaryObject();
            doc.Set("addresses", addresses);

            IDictionaryObject shipping = new DictionaryObject();
            shipping.Set("street", "1 Main street")
                .Set("city", "Mountain View")
                .Set("state", "CA");
            addresses.Set("shipping", shipping);

            doc = SaveDocument(doc);

            shipping = doc.GetDictionary("addresses").GetDictionary("shipping");
            shipping.Set("zip", "94042");

            doc = SaveDocument(doc);

            var result = new Dictionary<string, object> {
                ["addresses"] = new Dictionary<string, object> {
                    ["shipping"] = new Dictionary<string, object> {
                        ["street"] = "1 Main street",
                        ["city"] = "Mountain View",
                        ["state"] = "CA",
                        ["zip"] = "94042"
                    }
                }
            };
            doc.ToDictionary().ShouldBeEquivalentTo(result, "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestUpdateDictionaryInArray()
        {
            var doc = new Document("doc1");
            var addresses = new ArrayObject();
            doc.Set("addresses", addresses);

            IDictionaryObject address1 = new DictionaryObject();
            address1.Set("street", "1 Main street")
                .Set("city", "Mountain View")
                .Set("state", "CA");
            addresses.Add(address1);

            IDictionaryObject address2 = new DictionaryObject();
            address2.Set("street", "1 Second street")
                .Set("city", "Palo Alto")
                .Set("state", "CA");
            addresses.Add(address2);

            doc = SaveDocument(doc);

            address1 = doc.GetArray("addresses").GetDictionary(0);
            address1.Set("street", "2 Main street");
            address1.Set("zip", "94042");

            address2 = doc.GetArray("addresses").GetDictionary(1);
            address2.Set("street", "2 Second street");
            address2.Set("zip", "94302");

            doc = SaveDocument(doc);
            var result = new Dictionary<string, object> {
                ["addresses"] = new[] {
                    new Dictionary<string, object> {
                        ["street"] = "2 Main street",
                        ["city"] = "Mountain View",
                        ["state"] = "CA",
                        ["zip"] = "94042"
                    },
                    new Dictionary<string, object> {
                        ["street"] = "2 Second street",
                        ["city"] = "Palo Alto",
                        ["state"] = "CA",
                        ["zip"] = "94302"
                    }
                }
            };
            doc.ToDictionary().ShouldBeEquivalentTo(result, "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestUpdateNestedArray()
        {
            var doc = new Document("doc1");
            var groups = new ArrayObject();
            doc.Set("groups", groups);

            IArray group1 = new ArrayObject {
                "a",
                "b",
                "c"
            };
            groups.Add(group1);

            IArray group2 = new ArrayObject {
                1,
                2,
                3
            };
            groups.Add(group2);

            doc = SaveDocument(doc);

            group1 = doc.GetArray("groups").GetArray(0);
            group1.Set(0, "d");
            group1.Set(1, "e");
            group1.Set(2, "f");

            group2 = doc.GetArray("groups").GetArray(1);
            group2.Set(0, 4);
            group2.Set(1, 5);
            group2.Set(2, 6);

            doc = SaveDocument(doc);
            var result = new Dictionary<string, object> {
                ["groups"] = new object[] {
                    new[] {"d", "e", "f"},
                    new[] {4, 5, 6}
                }
            };
            doc.ToDictionary().ShouldBeEquivalentTo(result, "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestUpdateArrayInDictionary()
        {
            var doc = new Document("doc1");
            var group1 = new DictionaryObject();
            IArray member1 = new ArrayObject {
                "a",
                "b",
                "c"
            };
            group1.Set("member", member1);
            doc.Set("group1", group1);

            var group2 = new DictionaryObject();
            IArray member2 = new ArrayObject {
                1,
                2,
                3
            };
            group2.Set("member", member2);
            doc.Set("group2", group2);

            doc = SaveDocument(doc);

            member1 = doc.GetDictionary("group1").GetArray("member");
            member1.Set(0, "d");
            member1.Set(1, "e");
            member1.Set(2, "f");

            member2 = doc.GetDictionary("group2").GetArray("member");
            member2.Set(0, 4);
            member2.Set(1, 5);
            member2.Set(2, 6);

            doc = SaveDocument(doc);

            var result = new Dictionary<string, object> {
                ["group1"] = new Dictionary<string, object> {
                    ["member"] = new[] {"d", "e", "f"}
                },
                ["group2"] = new Dictionary<string, object> {
                    ["member"] = new[] {4, 5, 6}
                }
            };
            doc.ToDictionary().ShouldBeEquivalentTo(result, "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestSetDictionaryToMultipleKeys()
        {
            var doc = new Document("doc1");
            var address = new DictionaryObject();
            address.Set("street", "1 Main street")
                .Set("city", "Mountain View")
                .Set("state", "CA");
            doc.Set("shipping", address);
            doc.Set("billing", address);

            doc.GetObject("shipping").Should().BeSameAs(address, "because that is the object that was stored");
            doc.GetObject("billing").Should().BeSameAs(address, "because that is the object that was stored");
            
            address.Set("zip", "94042");
            doc.GetDictionary("shipping")
                .GetString("zip")
                .Should()
                .Be("94042", "because the update should be received by both dictionaries");
            doc.GetDictionary("billing")
                .GetString("zip")
                .Should()
                .Be("94042", "because the update should be received by both dictionaries");

            doc = SaveDocument(doc);

            var shipping = doc.GetDictionary("shipping");
            var billing = doc.GetDictionary("billing");

            shipping.Should().NotBeSameAs(address, "because the dictionaries should now be independent");
            billing.Should().NotBeSameAs(address, "because the dictionaries should now be independent");
            shipping.Should().NotBeSameAs(billing, "because the dictionaries should now be independent");

            shipping.Set("street", "2 Main street");
            billing.Set("street", "3 Main street");

            doc = SaveDocument(doc);

            doc.GetDictionary("shipping")
                .GetString("street")
                .Should()
                .Be("2 Main street", "because that was the final value stored");
            doc.GetDictionary("billing")
                .GetString("street")
                .Should()
                .Be("3 Main street", "because that was the final value stored");
        }

        [Fact]
        public void TestSetArrayObjectToMultipleKeys()
        {
            var doc = new Document("doc1");
            var phones = new ArrayObject {
                "650-000-0001",
                "650-000-0002"
            };

            doc.Set("mobile", phones);
            doc.Set("home", phones);

            phones.Add("650-000-0003");
            doc.GetArray("mobile")
                .Should()
                .ContainInOrder(new[] {"650-000-0001", "650-000-0002", "650-000-0003"},
                    "because both arrays should receive the update");
            doc.GetArray("home")
                .Should()
                .ContainInOrder(new[] { "650-000-0001", "650-000-0002", "650-000-0003" },
                    "because both arrays should receive the update");

            doc = SaveDocument(doc);

            var mobile = doc.GetArray("mobile");
            var home = doc.GetArray("home");
            mobile.Should().NotBeSameAs(phones, "because after save the arrays should be independent");
            home.Should().NotBeSameAs(phones, "because after save the arrays should be independent");
            mobile.Should().NotBeSameAs(home, "because after save the arrays should be independent");

            mobile.Add("650-000-1234");
            home.Add("650-000-5678");

            doc = SaveDocument(doc);

            doc.GetArray("mobile")
                .ToList()
                .Should()
                .ContainInOrder(new[] {"650-000-0001", "650-000-0002", "650-000-0003", "650-000-1234"},
                    "because otherwise the document is incorrect");
            doc.GetArray("home")
                .ToList()
                .Should()
                .ContainInOrder(new[] { "650-000-0001", "650-000-0002", "650-000-0003", "650-000-5678" },
                    "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestCount()
        {
            var doc = new Document("doc1");
            PopulateData(doc);

            doc.Count.Should().Be(11, "because that is the number of entries that were added");
            doc.Count.Should()
                .Be(doc.ToDictionary().Count, "because the count should not change when converting to dictionary");

            doc = SaveDocument(doc);

            doc.Count.Should().Be(11, "because that is the number of entries that were saved");
            doc.Count.Should()
                .Be(doc.ToDictionary().Count, "because the count should not change when converting to dictionary");
        }

        [Fact]
        public void TestRemoveKeys()
        {
            var doc = new Document("doc1");
            doc.Set(new Dictionary<string, object> {
                ["type"] = "profile",
                ["name"] = "Jason",
                ["weight"] = 130.5,
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 milky way.",
                    ["city"] = "galaxy city",
                    ["zip"] = 12345
                }
            });

            SaveDocument(doc);
            doc.Set("name", null);
            doc.Set("weight", null);
            doc.Set("age", null);
            doc.Set("active", null);
            doc.GetDictionary("address").Set("city", null);

            doc.GetString("name").Should().BeNull("because it was removed");
            doc.GetDouble("weight").Should().Be(0.0, "because it was removed");
            doc.GetLong("age").Should().Be(0L, "because it was removed");
            doc.GetBoolean("active").Should().BeFalse("because it was removed");

            doc.GetObject("name").Should().BeNull("because it was removed");
            doc.GetObject("weight").Should().BeNull("because it was removed");
            doc.GetObject("age").Should().BeNull("because it was removed");
            doc.GetObject("active").Should().BeNull("because it was removed");
            doc.GetDictionary("address").GetString("city").Should().BeNull("because it was removed");

            var address = doc.GetDictionary("address");
            doc.ToDictionary().ShouldBeEquivalentTo(new Dictionary<string, object> {
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
            doc.Set("type", null);
            doc.Set("address", null);
            doc.GetObject("type").Should().BeNull("because it was removed");
            doc.GetObject("address").Should().BeNull("because it was removed");
            doc.ToDictionary().Should().BeEmpty("because everything was removed");
        }


        [Fact]
        public void TestContainsKey()
        {
            var doc = new Document("doc1");
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

        [Fact(Skip ="Known failure")]
        public void TestDeleteNewDocument()
        {
            var doc = new Document("doc1");
            doc.Set("name", "Scott Tiger");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

            Db.Invoking(d => d.Delete(doc))
                .ShouldThrow<CouchbaseLiteException>()
                .Which.Code.Should()
                .Be(StatusCode.NotFound, "because deleting a non-existent document is invalid");
            doc.IsDeleted.Should().BeFalse("beacuse the document is still not deleted");
            doc.GetString("name").Should().Be("Scott Tiger", "because the delete was invalid");
        }

        [Fact]
        public void TestDelete()
        {
            var doc = new Document("doc1");
            doc.Set("name", "Scott Tiger");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not yet deleted");

            SaveDocument(doc);

            Db.Delete(doc);
            doc.GetObject("name").Should().BeNull("because the document was deleted");
            doc.ToDictionary().Should().BeEmpty("because the document was deleted");
            doc.IsDeleted.Should().BeTrue("because the document was deleted");
        }

        [Fact]
        public void TestDictionaryAfterDeleteDocument()
        {
            var dict = new Dictionary<string, object> {
                ["address"] = new Dictionary<string, object> { 
                    ["street"] = "1 Main street",
                    ["city"] = "Mountain View",
                    ["state"] = "CA"
                }
            };

            var doc = new Document("doc1", dict);
            SaveDocument(doc);

            var address = doc.GetDictionary("address");
            address.GetString("street").Should().Be("1 Main street", "because that is the street that was stored");
            address.GetString("city").Should().Be("Mountain View", "because that is the city that was stored");
            address.GetString("state").Should().Be("CA", "because that is the state that was stored");

            Db.Delete(doc);
            doc.GetDictionary("address").Should().BeNull("because the document was deleted");
            doc.ToDictionary().Should().BeEmpty("because the document was deleted");

            address.GetString("street").Should().Be("1 Main street", "because the dictionary is independent");
            address.GetString("city").Should().Be("Mountain View", "because the dictionary is independent");
            address.GetString("state").Should().Be("CA", "because the dictionary is independent");

            address.Set("zip", "94042");
            doc.GetDictionary("address")
                .Should()
                .BeNull("because changes to the dictionary shouldn't affect the document");
            doc.ToDictionary().Should().BeEmpty("because changes to the dictionary shouldn't affect the document");
        }

        [Fact]
        public void TestArrayAfterDeleteDocument()
        {
            var dict = new Dictionary<string, object> {
                ["members"] = new[] {"a", "b", "c"}
            };

            var doc = new Document("doc1", dict);
            SaveDocument(doc);

            var members = doc.GetArray("members");
            members.Count.Should().Be(3, "because three elements were added");
            members.ShouldBeEquivalentTo(dict["members"], "because otherwise the array has incorrect elements");

            Db.Delete(doc);
            doc.GetArray("members").Should().BeNull("because the document was deleted");
            doc.ToDictionary().Should().BeEmpty("because the document was deleted");

            members.Count.Should().Be(3, "because the array is independent of the document");
            members.ShouldBeEquivalentTo(dict["members"], "because the array is independent of the document");

            members.Set(2, "1");
            members.Add("2");

            doc.GetArray("members").Should().BeNull("because changes to the array shouldn't affect the document");
            doc.ToDictionary().Should().BeEmpty("because changes to the array shouldn't affect the document");
        }

        [Fact]
        public void TestPurge()
        {
            var doc = new Document("doc1");
            doc.Set("type", "profile");
            doc.Set("name", "Scott");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");
            doc.Exists.Should().BeFalse("because the document has not been saved yet");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

            // Purge before save:
            Db.Invoking(d => d.Purge(doc))
                .ShouldThrow<CouchbaseLiteException>()
                .Which.Code.Should().Be(StatusCode.NotFound,
                    "because purging a nonexisting revision is not valid");
            doc["type"].ToString().Should().Be("profile", "because the doc should still exist");
            doc["name"].ToString().Should().Be("Scott", "because the doc should still exist");

            // Save:
            SaveDocument(doc);
            doc.IsDeleted.Should().BeFalse("beacuse the document is still not deleted");

            // Purge:
            Db.Purge(doc);
            doc.IsDeleted.Should().BeFalse("because the document does not exist");
        }

        [Fact]
        public void TestReopenDB()
        {
            var doc = new Document("doc1");
            doc.Set("string", "str");
            Db.Save(doc);

            ReopenDB();

            doc = Db.GetDocument("doc1");
            doc.ToDictionary().Should().Equal(new Dictionary<string, object> { ["string"] = "str" }, "because otherwise the property didn't get saved");
            doc["string"].ToString().Should().Be("str", "because otherwise the property didn't get saved");
        }

        [Fact]
        public void TestBlob()
        {
            var content = Encoding.UTF8.GetBytes("12345");
            var data = new Blob("text/plain", content);
            var doc = new Document("doc1");
            doc.Set("data", data);
            doc.Set("name", "Jim");
            Db.Save(doc);

            using(var otherDb = new Database(Db.Name, Db.Options)) {
                var doc1 = otherDb.GetDocument("doc1");
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
            var doc = new Document("doc1");
            doc.Set("data", data);
            Db.Save(doc);

            using(var otherDb = new Database(Db.Name, Db.Options)) {
                var doc1 = otherDb.GetDocument("doc1");
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
            var doc = new Document("doc1");
            doc.Set("data", data);
            Db.Save(doc);

            using(var otherDb = new Database(Db.Name, Db.Options)) {
                var doc1 = otherDb.GetDocument("doc1");
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
            var doc = new Document("doc1");
            doc.Set("data", data);
            data = doc.GetBlob("data");
            for (int i = 0; i < 5; i++) {
                data.Content.Should().Equal(content, "because otherwise incorrect data was read");
                using (var contentStream = data.ContentStream) {
                    var buffer = new byte[10];
                    var bytesRead = contentStream.Read(buffer, 0, 10);
                    bytesRead.Should().Be(5, "because the data has 5 bytes");
                }
            }

            Db.Save(doc);
            
            using(var otherDb = new Database(Db.Name, Db.Options)) {
                var doc1 = otherDb.GetDocument("doc1");
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
            var doc = new Document("doc1");
            doc.Set("data", data);
            doc.Set("name", "Jim");
            Db.Save(doc);

            ReopenDB();

            doc = Db.GetDocument("doc1");
            doc.GetBlob("data").Content.Should().Equal(content, "because the data should have been retrieved correctly");

            ReopenDB();

            doc = Db.GetDocument("doc1");
            doc.Set("foo", "bar");
            Db.Save(doc);
            doc.GetBlob("data").Content.Should().Equal(content, "because the data should have been retrieved correctly");
        }

        private void PopulateData(Document doc)
        {
            var date = DateTimeOffset.Now;
            doc.Set("true", true)
                .Set("false", false)
                .Set("string", "string")
                .Set("zero", 0)
                .Set("one", 1)
                .Set("minus_one", -1)
                .Set("one_dot_one", 1.1)
                .Set("date", date);

            var dict = new DictionaryObject();
            dict.Set("street", "1 Main street")
                .Set("city", "Mountain View")
                .Set("state", "CA");
            doc.Set("dict", dict);

            var array = new ArrayObject();
            array.Add("650-123-0001");
            array.Add("650-123-0002");
            doc.Set("array", array);

            var content = Encoding.UTF8.GetBytes("12345");
            var blob = new Blob("text/plain", content);
            doc.Set("blob", blob);
        }
    }
}

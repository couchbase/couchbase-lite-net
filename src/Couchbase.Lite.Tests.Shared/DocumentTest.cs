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
            var doc = new MutableDocument();
            doc.Id.Should().NotBeNullOrEmpty("because every document should have an ID");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.ToDictionary().Should().BeEmpty("because the document has no properties");

            var savedDoc = Db.Save(doc);
            savedDoc.As<object>()
                .Should()
                .NotBeSameAs(doc, "because each call to GetDocument should return a unique instance");
            savedDoc.Id.Should().Be(doc.Id, "because the two document objects should have the same ID");
        }

        [Fact]
        public void TestCreateDocWithID()
        {
            var doc = new MutableDocument("doc1");
            doc.Id.Should().Be("doc1", "because that was the ID it was given");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.ToDictionary().Should().BeEmpty("because the document has no properties");

            var savedDoc = Db.Save(doc);
            savedDoc.As<object>()
                .Should()
                .NotBeSameAs(doc, "because each call to GetDocument should return a unique instance");
            savedDoc.Id.Should().Be(doc.Id, "because the two document objects should have the same ID");
        }

        [Fact]
        public void TestCreateDocWithEmptyStringID()
        {
            var doc = new MutableDocument("");
            doc.Id.Should().BeEmpty("because that was the ID it was given");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.ToDictionary().Should().BeEmpty("because the document has no properties");

            Db.Invoking(d => d.Save(doc))
                .ShouldThrow<LiteCoreException>()
                .Which.Error.Should()
                .Match<C4Error>(e => e.code == (int) C4ErrorCode.BadDocID &&
                                     e.domain == C4ErrorDomain.LiteCoreDomain);
        }

        [Fact]
        public void TestCreateDocWithNullID()
        {
            var doc = new MutableDocument(default(string));
            doc.Id.Should().NotBeNullOrEmpty("because every document should have an ID");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.ToDictionary().Should().BeEmpty("because the document has no properties");

            var savedDoc = Db.Save(doc);
            savedDoc.As<object>()
                .Should()
                .NotBeSameAs(doc, "because each call to GetDocument should return a unique instance");
            savedDoc.Id.Should().Be(doc.Id, "because the two document objects should have the same ID");
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

            var doc = new MutableDocument(dict);
            doc.Id.Should().NotBeNullOrEmpty("because every document should have an ID");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.ToDictionary().ShouldBeEquivalentTo(dict, "because the document was given properties");

            var savedDoc = Db.Save(doc);
            savedDoc.As<object>()
                .Should()
                .NotBeSameAs(doc, "because each call to GetDocument should return a unique instance");
            savedDoc.Id.Should().Be(doc.Id, "because the two document objects should have the same ID");
            savedDoc.ToDictionary().ShouldBeEquivalentTo(dict, "because the document was saved with properties");
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

            var doc = new MutableDocument("doc1", dict);
            doc.Id.Should().Be("doc1", "because that was the ID it was given");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.ToDictionary().ShouldBeEquivalentTo(dict, "because the document was given properties");

            var savedDoc = Db.Save(doc);
            savedDoc.As<object>()
                .Should()
                .NotBeSameAs(doc, "because each call to GetDocument should return a unique instance");
            savedDoc.Id.Should().Be(doc.Id, "because the two document objects should have the same ID");
            savedDoc.ToDictionary().ShouldBeEquivalentTo(dict, "because the document was saved with properties");
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

            var doc = new MutableDocument("doc1");
            doc.Set(dict);
            doc.ToDictionary().ShouldBeEquivalentTo(dict, "because that is what was just set");

            var savedDoc = Db.Save(doc);
            savedDoc.ToDictionary().ShouldBeEquivalentTo(dict);

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

            doc = savedDoc.ToMutable();
            doc.Set(nuDict);
            doc.ToDictionary().ShouldBeEquivalentTo(nuDict, "because that is what was just set");

            savedDoc = Db.Save(doc);
            savedDoc.ToDictionary().ShouldBeEquivalentTo(nuDict, "because that is what was just saved");
        }

        [Fact]
        public void TestGetValueFromDocument()
        {
            var doc = new MutableDocument("doc1");
            SaveDocument(doc, d =>
            {
                d.GetInt("key").Should().Be(0, "because no integer exists for this key");
                d.GetDouble("key").Should().Be(0.0, "because no double exists for this key");
                d.GetFloat("key").Should().Be(0.0f, "because no float exists for this key");
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
        public void TestSaveThenGetFromAnotherDB()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("name", "Scott Tiger");

            Db.Save(doc);

            using (var anotherDb = new Database(Db)) {
                var doc1b = anotherDb.GetDocument("doc1");
                doc1b.As<object>().Should().NotBeSameAs(doc, "because unique instances should be returned");
                doc.Id.Should().Be(doc1b.Id, "because object for the same document should have matching IDs");
                doc.ToDictionary().ShouldBeEquivalentTo(doc1b.ToDictionary(), "because the contents should match");
            }
        }

        [Fact]
        public void TestNoCacheNoLive()
        {
            var doc1a = new MutableDocument("doc1");
            doc1a.Set("name", "Scott Tiger");

            Db.Save(doc1a);

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

                var updatedDoc1b = doc1b.ToMutable();
                updatedDoc1b.Set("name", "Daniel Tiger");
                doc1b = Db.Save(updatedDoc1b);

                doc1b.Equals(doc1a).Should().BeFalse("because the contents should not match anymore");
                doc1b.Equals(doc1c).Should().BeFalse("because the contents should not match anymore");
                doc1b.Equals(doc1d).Should().BeFalse("because the contents should not match anymore");
            }
        }

        [Fact]
        public void TestSetString()
        {
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
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
        public void TestGetFloat()
        {
            var doc = new MutableDocument("doc1");
            PopulateData(doc);
            SaveDocument(doc, d =>
            {
                d.GetFloat("true").Should().Be(1.0f, "because a true bool value will be coalesced to 1.0f");
                d.GetFloat("false").Should().Be(0.0f, "because a false bool value will be coalesced to 0.0f");
                d.GetFloat("string").Should().Be(0.0f, "because that is the default value");
                d.GetFloat("zero").Should().Be(0.0f, "because zero was stored in this key");
                d.GetFloat("one").Should().Be(1.0f, "because one was stored in this key");
                d.GetFloat("minus_one").Should().Be(-1.0f, "because -1 was stored in this key");
                d.GetFloat("one_dot_one").Should().Be(1.1f, "because 1.1f was stored in this key");
                d.GetFloat("date").Should().Be(0.0f, "because that is the default value");
                d.GetFloat("dict").Should().Be(0.0f, "because that is the default value");
                d.GetFloat("array").Should().Be(0.0f, "because that is the default value");
                d.GetFloat("blob").Should().Be(0.0f, "because that is the default value");
                d.GetFloat("non_existing_key").Should().Be(0.0f, "because that key has no value");
            });
        }

        [Fact]
        public void TestSetGetMinMaxNumbers()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("min_int", Int64.MinValue);
            doc.Set("max_int", Int64.MaxValue);
            doc.Set("min_double", Double.MinValue);
            doc.Set("max_double", Double.MaxValue);
            doc.Set("min_float", Single.MinValue);
            doc.Set("max_float", Single.MaxValue);

            SaveDocument(doc, d =>
            {
                d.GetLong("min_int").Should().Be(Int64.MinValue, "because that is what was stored");
                d.GetLong("max_int").Should().Be(Int64.MaxValue, "because that is what was stored");
                d.GetDouble("min_double").Should().Be(Double.MinValue, "because that is what was stored");
                d.GetDouble("max_double").Should().Be(Double.MaxValue, "because that is what was stored");
                d.GetFloat("min_float").Should().Be(Single.MinValue, "because that is what was stored");
                d.GetFloat("max_float").Should().Be(Single.MaxValue, "because that is what was stored");
            });
        }

        [Fact]
        public void TestSetBoolean()
        {
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
            IMutableDictionary dict = new MutableDictionary();
            dict.Set("street", "1 Main street");
            doc.Set("dict", dict);

            doc.GetObject("dict").Should().Be(dict, "because that is what was stored");
            var savedDoc = Db.Save(doc);

            savedDoc.GetObject("dict").Should().NotBeSameAs(dict, "beacuse a new MutableDocument should return a new object");
            savedDoc.GetObject("dict")
                .Should()
                .BeSameAs(savedDoc.GetDictionary("dict"), "because the same document should return the same thing");
            savedDoc.GetDictionary("dict")
                .ToDictionary()
                .ShouldBeEquivalentTo(dict.ToDictionary(), "because the contents should be the same");

            doc = savedDoc.ToMutable();
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

            savedDoc = Db.Save(doc);
            savedDoc.GetObject("dict").Should().NotBeSameAs(dict, "beacuse a new MutableDocument should return a new object");
            savedDoc.GetObject("dict")
                .Should()
                .BeSameAs(savedDoc.GetDictionary("dict"), "because the same document should return the same thing");
            savedDoc.GetDictionary("dict")
                .ToDictionary()
                .ShouldBeEquivalentTo(csharpDict, "because the contents should be the same as before");
        }

        [Fact]
        public void TestGetDictionary()
        {
            var doc = new MutableDocument("doc1");
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
            var doc = new MutableDocument("doc1");
            IMutableArray array = new MutableArray {
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

            var savedDoc = Db.Save(doc);

            savedDoc.GetObject("array").Should().NotBeSameAs(array, "because a new MutableDocument should return a new object");
            savedDoc.GetObject("array")
                .Should()
                .BeSameAs(savedDoc.GetArray("array"), "because the same doc should return the same object");
            savedDoc.GetArray("array")
                .Should()
                .ContainInOrder(new[] { "item1", "item2", "item3" }, "because otherwise the contents are incorrect");

            doc = savedDoc.ToMutable();
            array = doc.GetArray("array");
            array.Add("item4");
            array.Add("item5");

            savedDoc = Db.Save(doc);
            savedDoc.GetObject("array").Should().NotBeSameAs(array, "because a new MutableDocument should return a new object");
            savedDoc.GetObject("array")
                .Should()
                .BeSameAs(savedDoc.GetArray("array"), "because the same doc should return the same object");
            savedDoc.GetArray("array")
                .Should()
                .ContainInOrder(new[] { "item1", "item2", "item3", "item4", "item5" },
                "because otherwise the contents are incorrect");
        }

        [Fact]
        public void TestGetArray()
        {
            var doc = new MutableDocument("doc1");
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
        public void TestSetNull()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("null", default(object));
            SaveDocument(doc, d =>
            {
                d.GetObject("null").Should().BeNull("because that is what was stored");
                d.Count.Should().Be(1, "because the value is null, not missing");
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

            var doc = new MutableDocument("doc1");
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

            var savedDoc = Db.Save(doc);

            nuDict["zip"] = "94302";
            savedDoc.ToDictionary()
                .ShouldBeEquivalentTo(new Dictionary<string, object> {
                    ["address"] = nuDict
                }, "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestSetCSharpList()
        {
            var array = new[] {"a", "b", "c"};
            var doc = new MutableDocument("doc1");
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

            var savedDoc = Db.Save(doc);

            savedDoc.ToDictionary()
                .ShouldBeEquivalentTo(new Dictionary<string, object> {
                    ["members"] = new[] {"d", "e", "f", "g"}
                }, "beacuse otherwise the document contents are incorrect");
        }

        [Fact]
        public void TestUpdateNestedDictionary()
        {
            var doc = new MutableDocument("doc1");
            var addresses = new MutableDictionary();
            doc.Set("addresses", addresses);

            IMutableDictionary shipping = new MutableDictionary();
            shipping.Set("street", "1 Main street")
                .Set("city", "Mountain View")
                .Set("state", "CA");
            addresses.Set("shipping", shipping);

            var savedDoc = Db.Save(doc);
            doc = savedDoc.ToMutable();

            shipping = doc.GetDictionary("addresses").GetDictionary("shipping");
            shipping.Set("zip", "94042");

            savedDoc = Db.Save(doc);

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
            savedDoc.ToDictionary().ShouldBeEquivalentTo(result, "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestUpdateDictionaryInArray()
        {
            var doc = new MutableDocument("doc1");
            var addresses = new MutableArray();
            doc.Set("addresses", addresses);

            IMutableDictionary address1 = new MutableDictionary();
            address1.Set("street", "1 Main street")
                .Set("city", "Mountain View")
                .Set("state", "CA");
            addresses.Add(address1);

            IMutableDictionary address2 = new MutableDictionary();
            address2.Set("street", "1 Second street")
                .Set("city", "Palo Alto")
                .Set("state", "CA");
            addresses.Add(address2);

            doc = Db.Save(doc).ToMutable();

            address1 = doc.GetArray("addresses").GetDictionary(0);
            address1.Set("street", "2 Main street");
            address1.Set("zip", "94042");

            address2 = doc.GetArray("addresses").GetDictionary(1);
            address2.Set("street", "2 Second street");
            address2.Set("zip", "94302");

            var savedDoc = Db.Save(doc);
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
            savedDoc.ToDictionary().ShouldBeEquivalentTo(result, "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestUpdateNestedArray()
        {
            var doc = new MutableDocument("doc1");
            var groups = new MutableArray();
            doc.Set("groups", groups);

            IMutableArray group1 = new MutableArray {
                "a",
                "b",
                "c"
            };
            groups.Add(group1);

            IMutableArray group2 = new MutableArray {
                1,
                2,
                3
            };
            groups.Add(group2);

            doc = Db.Save(doc).ToMutable();

            group1 = doc.GetArray("groups").GetArray(0);
            group1.Set(0, "d");
            group1.Set(1, "e");
            group1.Set(2, "f");

            group2 = doc.GetArray("groups").GetArray(1);
            group2.Set(0, 4);
            group2.Set(1, 5);
            group2.Set(2, 6);

            var savedDoc = Db.Save(doc);
            var result = new Dictionary<string, object> {
                ["groups"] = new object[] {
                    new[] {"d", "e", "f"},
                    new[] {4, 5, 6}
                }
            };
            savedDoc.ToDictionary().ShouldBeEquivalentTo(result, "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestUpdateArrayInDictionary()
        {
            var doc = new MutableDocument("doc1");
            var group1 = new MutableDictionary();
            IMutableArray member1 = new MutableArray {
                "a",
                "b",
                "c"
            };
            group1.Set("member", member1);
            doc.Set("group1", group1);

            var group2 = new MutableDictionary();
            IMutableArray member2 = new MutableArray {
                1,
                2,
                3
            };
            group2.Set("member", member2);
            doc.Set("group2", group2);

            doc = Db.Save(doc).ToMutable();

            member1 = doc.GetDictionary("group1").GetArray("member");
            member1.Set(0, "d");
            member1.Set(1, "e");
            member1.Set(2, "f");

            member2 = doc.GetDictionary("group2").GetArray("member");
            member2.Set(0, 4);
            member2.Set(1, 5);
            member2.Set(2, 6);

            var savedDoc = Db.Save(doc);

            var result = new Dictionary<string, object> {
                ["group1"] = new Dictionary<string, object> {
                    ["member"] = new[] {"d", "e", "f"}
                },
                ["group2"] = new Dictionary<string, object> {
                    ["member"] = new[] {4, 5, 6}
                }
            };
            savedDoc.ToDictionary().ShouldBeEquivalentTo(result, "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestSetDictionaryToMultipleKeys()
        {
            var doc = new MutableDocument("doc1");
            var address = new MutableDictionary();
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

            doc = Db.Save(doc).ToMutable();

            var shipping = doc.GetDictionary("shipping");
            var billing = doc.GetDictionary("billing");

            shipping.Should().NotBeSameAs(address, "because the dictionaries should now be independent");
            billing.Should().NotBeSameAs(address, "because the dictionaries should now be independent");
            shipping.Should().NotBeSameAs(billing, "because the dictionaries should now be independent");

            shipping.Set("street", "2 Main street");
            billing.Set("street", "3 Main street");

            var savedDoc = Db.Save(doc);

            savedDoc.GetDictionary("shipping")
                .GetString("street")
                .Should()
                .Be("2 Main street", "because that was the final value stored");
            savedDoc.GetDictionary("billing")
                .GetString("street")
                .Should()
                .Be("3 Main street", "because that was the final value stored");
        }

        [Fact]
        public void TestSetArrayObjectToMultipleKeys()
        {
            var doc = new MutableDocument("doc1");
            var phones = new MutableArray {
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

            doc = Db.Save(doc).ToMutable();

            var mobile = doc.GetArray("mobile");
            var home = doc.GetArray("home");
            mobile.Should().NotBeSameAs(phones, "because after save the arrays should be independent");
            home.Should().NotBeSameAs(phones, "because after save the arrays should be independent");
            mobile.Should().NotBeSameAs(home, "because after save the arrays should be independent");

            mobile.Add("650-000-1234");
            home.Add("650-000-5678");

            var savedDoc = Db.Save(doc);

            savedDoc.GetArray("mobile")
                .ToList()
                .Should()
                .ContainInOrder(new[] {"650-000-0001", "650-000-0002", "650-000-0003", "650-000-1234"},
                    "because otherwise the document is incorrect");
            savedDoc.GetArray("home")
                .ToList()
                .Should()
                .ContainInOrder(new[] { "650-000-0001", "650-000-0002", "650-000-0003", "650-000-5678" },
                    "because otherwise the document is incorrect");
        }

        [Fact]
        public void TestCount()
        {
            var doc = new MutableDocument("doc1");
            PopulateData(doc);

            doc.Count.Should().Be(11, "because that is the number of entries that were added");
            doc.Count.Should()
                .Be(doc.ToDictionary().Count, "because the count should not change when converting to dictionary");

            var savedDoc = Db.Save(doc);

            savedDoc.Count.Should().Be(11, "because that is the number of entries that were saved");
            savedDoc.Count.Should()
                .Be(doc.ToDictionary().Count, "because the count should not change when converting to dictionary");
        }

        [Fact]
        public void TestRemoveKeys()
        {
            var doc = new MutableDocument("doc1");
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

            doc = Db.Save(doc).ToMutable();
            doc.Remove("name");
            doc.Remove("weight");
            doc.Remove("age");
            doc.Remove("active");
            doc.GetDictionary("address").Remove("city");

            doc.GetString("name").Should().BeNull("because it was removed");
            doc.GetDouble("weight").Should().Be(0.0, "because it was removed");
            doc.GetFloat("weight").Should().Be(0.0f, "because it was removed");
            doc.GetLong("age").Should().Be(0L, "because it was removed");
            doc.GetBoolean("active").Should().BeFalse("because it was removed");

            doc.GetObject("name").Should().BeNull("because it was removed");
            doc.GetObject("weight").Should().BeNull("because it was removed");
            doc.GetObject("age").Should().BeNull("because it was removed");
            doc.GetObject("active").Should().BeNull("because it was removed");
            doc.GetDictionary("address").GetString("city").Should().BeNull("because it was removed");

            doc.Contains("name").Should().BeFalse("because that key was removed");
            doc.Contains("weight").Should().BeFalse("because that key was removed");
            doc.Contains("age").Should().BeFalse("because that key was removed");
            doc.Contains("active").Should().BeFalse("because that key was removed");
            doc.GetDictionary("address").Contains("city").Should().BeFalse("because that key was removed");

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
            doc.Remove("type");
            doc.Remove("address");
            doc.GetObject("type").Should().BeNull("because it was removed");
            doc.GetObject("address").Should().BeNull("because it was removed");
            doc.ToDictionary().Should().BeEmpty("because everything was removed");
        }

        [Fact]
        public void TestRemoveKeysBySettingDictionary()
        {
            var props = new Dictionary<string, object> {
                ["PropName1"] = "Val1",
                ["PropName2"] = 42
            };

            var newDoc = new MutableDocument("docName", props);
            Db.Save(newDoc);

            var newProps = new Dictionary<string, object> {
                ["PropName3"] = "Val3",
                ["PropName4"] = 84
            };

            var existingDoc = Db.GetDocument("docName").ToMutable();
            existingDoc.Set(newProps);
            Db.Save(existingDoc);

            existingDoc.ToDictionary().ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["PropName3"] = "Val3",
                ["PropName4"] = 84
            });
        }

        [Fact]
        public void TestContainsKey()
        {
            var doc = new MutableDocument("doc1");
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

        //[Fact] known failure
        public void TestDeleteNewDocument()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("name", "Scott Tiger");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

            Db.Invoking(d => d.Delete(doc))
                .ShouldThrow<CouchbaseLiteException>()
                .Which.Status.Should()
                .Be(StatusCode.NotFound, "because deleting a non-existent document is invalid");
            doc.IsDeleted.Should().BeFalse("beacuse the document is still not deleted");
            doc.GetString("name").Should().Be("Scott Tiger", "because the delete was invalid");
        }

        [Fact]
        public void TestDelete()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("name", "Scott Tiger");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not yet deleted");

            var savedDoc = Db.Save(doc);

            Db.Delete(savedDoc);

            savedDoc = Db.GetDocument(doc.Id);
            savedDoc.GetObject("name").Should().BeNull("because the document was deleted");
            savedDoc.ToDictionary().Should().BeEmpty("because the document was deleted");
            savedDoc.IsDeleted.Should().BeTrue("because the document was deleted");
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

            var doc = new MutableDocument("doc1", dict);
            var savedDoc = Db.Save(doc);

            var address = savedDoc.GetDictionary("address");
            address.GetString("street").Should().Be("1 Main street", "because that is the street that was stored");
            address.GetString("city").Should().Be("Mountain View", "because that is the city that was stored");
            address.GetString("state").Should().Be("CA", "because that is the state that was stored");

            Db.Delete(savedDoc);

            address.GetString("street").Should().Be("1 Main street", "because the dictionary is independent");
            address.GetString("city").Should().Be("Mountain View", "because the dictionary is independent");
            address.GetString("state").Should().Be("CA", "because the dictionary is independent");
        }

        [Fact]
        public void TestArrayAfterDeleteDocument()
        {
            var dict = new Dictionary<string, object> {
                ["members"] = new[] {"a", "b", "c"}
            };

            var doc = new MutableDocument("doc1", dict);
            var savedDoc = Db.Save(doc);

            var members = savedDoc.GetArray("members");
            members.Count.Should().Be(3, "because three elements were added");
            members.ShouldBeEquivalentTo(dict["members"], "because otherwise the array has incorrect elements");

            Db.Delete(savedDoc);

            members.Count.Should().Be(3, "because the array is independent of the document");
            members.ShouldBeEquivalentTo(dict["members"], "because the array is independent of the document");
        }

        [Fact]
        public void TestPurge()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("type", "profile");
            doc.Set("name", "Scott");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

            // Purge before save:
            Db.Invoking(d => d.Purge(doc))
                .ShouldThrow<CouchbaseLiteException>()
                .Which.Status.Should().Be(StatusCode.NotFound,
                    "because purging a nonexisting revision is not valid");

            // Save:
            var savedDoc = Db.Save(doc);

            // Purge should not throw:
            Db.Purge(savedDoc);
        }

        [Fact]
        public void TestReopenDB()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("string", "str");
            Db.Save(doc);

            ReopenDB();

            var gotDoc = Db.GetDocument("doc1");
            gotDoc.ToDictionary().Should().Equal(new Dictionary<string, object> { ["string"] = "str" }, "because otherwise the property didn't get saved");
            gotDoc["string"].ToString().Should().Be("str", "because otherwise the property didn't get saved");
        }

        [Fact]
        public void TestBlob()
        {
            var content = Encoding.UTF8.GetBytes("12345");
            var data = new Blob("text/plain", content);
            var doc = new MutableDocument("doc1");
            doc.Set("data", data);
            doc.Set("name", "Jim");
            Db.Save(doc);

            using(var otherDb = new Database(Db.Name, Db.Config)) {
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

            var stream = new MemoryStream(content);
            data = new Blob("text/plain", stream);
            data.Content.SequenceEqual(content).Should().BeTrue();
        }

        [Fact]
        public void TestEmptyBlob()
        {
            var content = new byte[0];
            var data = new Blob("text/plain", content);
            var doc = new MutableDocument("doc1");
            doc.Set("data", data);
            Db.Save(doc);

            using(var otherDb = new Database(Db.Name, Db.Config)) {
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
            var doc = new MutableDocument("doc1");
            doc.Set("data", data);
            Db.Save(doc);

            using(var otherDb = new Database(Db.Name, Db.Config)) {
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
            var doc = new MutableDocument("doc1");
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
            
            using(var otherDb = new Database(Db.Name, Db.Config)) {
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
            var doc = new MutableDocument("doc1");
            doc.Set("data", data);
            doc.Set("name", "Jim");
            Db.Save(doc);

            ReopenDB();

            var gotDoc = Db.GetDocument("doc1");
            gotDoc.GetBlob("data").Content.Should().Equal(content, "because the data should have been retrieved correctly");

            ReopenDB();

            doc = Db.GetDocument("doc1").ToMutable();
            doc.Set("foo", "bar");
            Db.Save(doc);
            doc.GetBlob("data").Content.Should().Equal(content, "because the data should have been retrieved correctly");
        }

        [Fact]
        public void TestEnumeratingDocument()
        {
            var doc = new MutableDocument("doc1");
            for (int i = 0; i < 20; i++)
            {
                doc.Set($"key{i}", i);
            }

            var content = doc.ToDictionary();
            var result = new Dictionary<string, object>();
            foreach (var item in doc)
            {
                result[item.Key] = item.Value;
            }

            result.ShouldBeEquivalentTo(content, "because that is the correct content");
            content = doc.Remove("key2").Set("key20", 20).Set("key21", 21).ToDictionary();

            result = new Dictionary<string, object>();
            foreach (var item in doc)
            {
                result[item.Key] = item.Value;
            }

            result.ShouldBeEquivalentTo(content, "because that is the correct content");

            SaveDocument(doc, d =>
            {
                result = new Dictionary<string, object>();
                foreach (var item in d)
                {
                    result[item.Key] = item.Value;
                }

                result.ShouldBeEquivalentTo(content, "because that is the correct content");
            });
        }

        private void PopulateData(MutableDocument doc)
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

            var dict = new MutableDictionary();
            dict.Set("street", "1 Main street")
                .Set("city", "Mountain View")
                .Set("state", "CA");
            doc.Set("dict", dict);

            var array = new MutableArray();
            array.Add("650-123-0001");
            array.Add("650-123-0002");
            doc.Set("array", array);

            var content = Encoding.UTF8.GetBytes("12345");
            var blob = new Blob("text/plain", content);
            doc.Set("blob", blob);
        }
    }
}

//
//  DocumentTest.cs
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
        private const string Blob = "i'm blob";

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

            SaveDocument(doc);
        }

        [Fact]
        public void TestCreateDocWithID()
        {
            var doc = new MutableDocument("doc1");
            doc.Id.Should().Be("doc1", "because that was the ID it was given");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.ToDictionary().Should().BeEmpty("because the document has no properties");

            SaveDocument(doc);
        }

        [Fact]
        public void TestCreateDocWithEmptyStringID()
        {
            var doc = new MutableDocument("");
            doc.Id.Should().BeEmpty("because that was the ID it was given");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.ToDictionary().Should().BeEmpty("because the document has no properties");

            Db.Invoking(d => d.Save(doc))
                .ShouldThrow<CouchbaseLiteException>()
                .Where(e => e.Error == CouchbaseLiteError.BadDocID &&
                                     e.Domain == CouchbaseLiteErrorType.CouchbaseLite);
        }

        [Fact]
        public void TestCreateDocWithNullID()
        {
            var doc = new MutableDocument(default(string));
            doc.Id.Should().NotBeNullOrEmpty("because every document should have an ID");
            doc.IsDeleted.Should().BeFalse("because the document is not deleted");
            doc.ToDictionary().Should().BeEmpty("because the document has no properties");

            SaveDocument(doc);
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

            SaveDocument(doc);
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

            SaveDocument(doc);
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
            doc.SetData(dict);
            doc.ToDictionary().ShouldBeEquivalentTo(dict, "because that is what was just set");

            SaveDocument(doc);

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
            
            doc.SetData(nuDict);
            doc.ToDictionary().ShouldBeEquivalentTo(nuDict, "because that is what was just set");

            SaveDocument(doc);
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
                d.GetValue("key").Should().BeNull("because no object exists for this key");
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
            doc.SetString("name", "Scott Tiger");

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
            doc1a.SetString("name", "Scott Tiger");
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

                var updatedDoc1b = doc1b.ToMutable();
                updatedDoc1b.SetString("name", "Daniel Tiger");
                SaveDocument(updatedDoc1b);

                updatedDoc1b.Equals(doc1a).Should().BeFalse("because the contents should not match anymore");
                updatedDoc1b.Equals(doc1c).Should().BeFalse("because the contents should not match anymore");
                updatedDoc1b.Equals(doc1d).Should().BeFalse("because the contents should not match anymore");
            }
        }

        [Fact]
        public void TestSetString()
        {
            var doc = new MutableDocument("doc1");
            doc.SetString("string1", "");
            doc.SetString("string2", "string");

            SaveDocument(doc, d =>
            {
                d.GetString("string1").Should().Be("", "because that is the value of the first revision of string1");
                d.GetString("string2")
                    .Should()
                    .Be("string", "because that is the value of the first revision of string2");
            });

            doc.Dispose();
            doc = Db.GetDocument("doc1").ToMutable();
            doc.SetString("string2", "");
            doc.SetString("string1", "string");

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
            doc.SetInt("number1", 1);
            doc.SetInt("number2", 0);
            doc.SetInt("number3", -1);
            doc.SetDouble("number4", 1.1);

            SaveDocument(doc, d =>
            {
                d.GetInt("number1").Should().Be(1, "because that is the value of the first revision of number1");
                d.GetInt("number2").Should().Be(0, "because that is the value of the first revision of number2");
                d.GetInt("number3").Should().Be(-1, "because that is the value of the first revision of number3");
                d.GetDouble("number4").Should().Be(1.1, "because that is the value of the first revision of number4");
            });

            doc.Dispose();
            doc = Db.GetDocument("doc1").ToMutable();
            doc.SetInt("number1", 0);
            doc.SetInt("number2", 1);
            doc.SetDouble("number3", 1.1);
            doc.SetInt("number4", -1);

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
        public void TestGetLong()
        {
            var doc = new MutableDocument("doc1");
            PopulateData(doc);
            SaveDocument(doc, d =>
            {
                d.GetLong("true").Should().Be(1L, "because a true bool value will be coalesced to 1L");
                d.GetLong("false").Should().Be(0L, "because a false bool value will be coalesced to 0L");
                d.GetLong("string").Should().Be(0L, "because that is the default value");
                d.GetLong("zero").Should().Be(0L, "because zero was stored in this key");
                d.GetLong("one").Should().Be(1L, "because one was stored in this key");
                d.GetLong("minus_one").Should().Be(-1L, "because -1L was stored in this key");
                d.GetLong("one_dot_one").Should().Be(1L, "because 1L.1L gets truncated to 1L");
                d.GetLong("date").Should().Be(0L, "because that is the default value");
                d.GetLong("dict").Should().Be(0L, "because that is the default value");
                d.GetLong("array").Should().Be(0L, "because that is the default value");
                d.GetLong("blob").Should().Be(0L, "because that is the default value");
                d.GetLong("non_existing_key").Should().Be(0L, "because that key has no value");
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
            doc.SetLong("min_int", Int64.MinValue);
            doc.SetLong("max_int", Int64.MaxValue);
            doc.SetDouble("min_double", Double.MinValue);
            doc.SetDouble("max_double", Double.MaxValue);
            doc.SetFloat("min_float", Single.MinValue);
            doc.SetFloat("max_float", Single.MaxValue);

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
        public void TestSetGetFloatNumbers()
        {
            using (var doc = new MutableDocument("doc1")) {
                doc.SetFloat("number1", 1.00f)
                    .SetFloat("number2", 1.49f)
                    .SetFloat("number3", 1.50f)
                    .SetFloat("number4", 1.51f)
                    .SetDouble("number5", 1.99);

                SaveDocument(doc, d =>
                {
                    d.GetInt("number1").Should().Be(1);
                    d.GetFloat("number1").Should().Be(1.00f);
                    d.GetDouble("number1").Should().Be(1.00);

                    d.GetInt("number2").Should().Be(1);
                    d.GetFloat("number2").Should().Be(1.49f);
                    d.GetDouble("number2").Should().BeApproximately(1.49, 0.00001);

                    d.GetInt("number3").Should().Be(1);
                    d.GetFloat("number3").Should().Be(1.50f);
                    d.GetDouble("number3").Should().BeApproximately(1.50, 0.00001);

                    d.GetInt("number4").Should().Be(1);
                    d.GetFloat("number4").Should().Be(1.51f);
                    d.GetDouble("number4").Should().BeApproximately(1.51, 0.00001);

                    d.GetInt("number5").Should().Be(1);
                    d.GetFloat("number5").Should().Be(1.99f);
                    d.GetDouble("number5").Should().BeApproximately(1.99, 0.00001);
                });
            }
        }

        [Fact]
        public void TestSetBoolean()
        {
            var doc = new MutableDocument("doc1");
            doc.SetBoolean("boolean1", true);
            doc.SetBoolean("boolean2", false);

            SaveDocument(doc, d =>
            {
                d.GetBoolean("boolean1").Should().Be(true, "because that is the value of the first revision of boolean1");
                d.GetBoolean("boolean2").Should().Be(false, "because that is the value of the first revision of boolean2");
            });

            doc.Dispose();
            doc = Db.GetDocument("doc1").ToMutable();
            doc.SetBoolean("boolean1", false);
            doc.SetBoolean("boolean2", true);

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
            doc.SetString("date", dateStr);

            SaveDocument(doc, d =>
            {
                d.GetValue("date").Should().Be(dateStr, "because that is what was stored");
                d.GetString("date").Should().Be(dateStr, "because a string was stored");
                d.GetDate("date").Should().Be(date, "because the string is convertible to a date");
            });

            doc.Dispose();
            doc = Db.GetDocument("doc1").ToMutable();
            var nuDate = date + TimeSpan.FromSeconds(60);
            var nuDateStr = nuDate.ToString("o");
            doc.SetDate("date", nuDate);
            
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
            doc.SetBlob("blob", blob);

            SaveDocument(doc, d =>
            {
                d.GetValue("blob")
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

            doc.Dispose();
            doc = Db.GetDocument("doc1").ToMutable();
            var nuContent = Encoding.UTF8.GetBytes("1234567890");
            var nuBlob = new Blob("text/plain", nuContent);
            doc.SetBlob("blob", nuBlob);

            SaveDocument(doc, d =>
            {
                d.GetValue("blob")
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
            var dict = new MutableDictionaryObject();
            dict.SetString("street", "1 Main street");
            doc.SetDictionary("dict", dict);
            doc.GetValue("dict").Should().Be(dict, "because that is what was stored");
            SaveDocument(doc, d => { d.GetDictionary("dict").ShouldBeEquivalentTo(dict); });

            dict = doc.GetDictionary("dict");
            dict.SetString("city", "Mountain View");
            SaveDocument(doc, d => { d.GetDictionary("dict").ShouldBeEquivalentTo(dict); });
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
        public void TestSetArray()
        {
            var doc = new MutableDocument("doc1");
            var array = new MutableArrayObject();
            array.AddString("item1").AddString("item2").AddString("item3");

            doc.SetArray("array", array);

            doc.GetValue("array").Should().Be(array, "because that is what was stored");
            doc.GetArray("array").As<object>().Should().Be(array, "because that is what was stored");
            doc.GetArray("array")
                .Should()
                .ContainInOrder(new[] {"item1", "item2", "item3"}, "because otherwise the contents are incorrect");

            SaveDocument(doc, d => { d.GetArray("array").ToList().Should().ContainInOrder(array.ToList()); });

            array = doc.GetArray("array");
            array.AddString("item4");
            array.AddString("item5");
            SaveDocument(doc, d => { d.GetArray("array").ToList().Should().ContainInOrder(array.ToList()); });
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
            doc.SetValue("null", null);
            SaveDocument(doc, d =>
            {
                d.GetValue("null").Should().BeNull("because that is what was stored");
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
            doc.SetValue("address", dict);

            var address = doc.GetDictionary("address");
            address.As<object>()
                .Should().NotBeNull().And
                .BeSameAs(doc.GetValue("address"), "because the same doc should return the same object");
            address.GetString("street").Should().Be("1 Main street", "because that is the street that was stored");
            address.GetString("city").Should().Be("Mountain View", "because that is the city that was stored");
            address.GetString("state").Should().Be("CA", "because that is the state that was stored");
            address.ToDictionary().ShouldBeEquivalentTo(dict, "because the content should be the same");

            var nuDict = new Dictionary<string, object> {
                ["street"] = "1 Second street",
                ["city"] = "Palo Alto",
                ["state"] = "CA"
            };
            doc.SetValue("address", nuDict);

            // Make sure the old address dictionary is not affected
            address.Should().NotBeSameAs(doc.GetDictionary("address"), "because address is now detached");
            address.GetString("street").Should().Be("1 Main street", "because that is the street that was stored");
            address.GetString("city").Should().Be("Mountain View", "because that is the city that was stored");
            address.GetString("state").Should().Be("CA", "because that is the state that was stored");
            address.ToDictionary().ShouldBeEquivalentTo(dict, "because the content should be the same");
            var nuAddress = doc.GetDictionary("address");
            nuAddress.Should().NotBeSameAs(address, "beacuse they are two different entities");

            nuAddress.SetString("zip", "94302");
            nuAddress.GetString("zip").Should().Be("94302", "because that was what was just stored");
            address.GetString("zip").Should().BeNull("because address should not be affected");

            nuDict["zip"] = "94302";
            SaveDocument(doc, d => { d.GetDictionary("address").ShouldBeEquivalentTo(nuDict); });
        }

        [Fact]
        public void TestSetCSharpList()
        {
            var array = new[] {"a", "b", "c"};
            var doc = new MutableDocument("doc1");
            doc.SetValue("members", array);

            var members = doc.GetArray("members");
            members.As<object>()
                .Should()
                .NotBeNull()
                .And
                .BeSameAs(doc.GetValue("members"), "because the same document should return the same object");

            members.Count.Should().Be(3, "because there are three elements inside");
            members.Should().ContainInOrder(array, "because otherwise the contents are wrong");
            members.ToArray().Should().ContainInOrder(array, "because otherwise the contents are wrong");

            var nuArray = new[] {"d", "e", "f"};
            doc.SetValue("members", nuArray);

            // Make sure the old members array is not affected
            members.Count.Should().Be(3, "because there are three elements inside");
            members.Should().ContainInOrder(array, "because otherwise the contents are wrong");
            members.ToArray().Should().ContainInOrder(array, "because otherwise the contents are wrong");

            var nuMembers = doc.GetArray("members");
            members.Should().NotBeSameAs(nuMembers, "because the new array should have no relation to the old");
            nuMembers.AddString("g");
            nuMembers.Count.Should().Be(4, "because another element was added");
            nuMembers.GetValue(3).Should().Be("g", "because that is what was added");
            members.Count.Should().Be(3, "beacuse members still has three elements");

            SaveDocument(doc, d =>
            {
                d.ToDictionary()
                    .ShouldBeEquivalentTo(new Dictionary<string, object> { ["members"] = new[] { "d", "e", "f", "g" } },
                        "beacuse otherwise the document contents are incorrect");
            });
        }

        [Fact]
        public void TestUpdateNestedDictionary()
        {
            var doc = new MutableDocument("doc1");
            var addresses = new MutableDictionaryObject();
            doc.SetDictionary("addresses", addresses);

            var shipping = new MutableDictionaryObject();
            shipping.SetString("street", "1 Main street")
                .SetString("city", "Mountain View")
                .SetString("state", "CA");
            addresses.SetDictionary("shipping", shipping);

            SaveDocument(doc, d =>
            {
                d.ToDictionary().ShouldBeEquivalentTo(new Dictionary<string, object>
                {
                    ["addresses"] = new Dictionary<string, object>
                    {
                        ["shipping"] = new Dictionary<string, object>
                        {
                            ["street"] = "1 Main street",
                            ["city"] = "Mountain View",
                            ["state"] = "CA"
                        }
                    }
                });
            });

            var gotShipping = doc.GetDictionary("addresses").GetDictionary("shipping");
            gotShipping.SetString("zip", "94042");

            SaveDocument(doc, d =>
            {
                d.ToDictionary().ShouldBeEquivalentTo(new Dictionary<string, object>
                {
                    ["addresses"] = new Dictionary<string, object>
                    {
                        ["shipping"] = new Dictionary<string, object>
                        {
                            ["street"] = "1 Main street",
                            ["city"] = "Mountain View",
                            ["state"] = "CA",
                            ["zip"] = "94042"
                        }
                    }
                });
            });
        }

        [Fact]
        public void TestUpdateDictionaryInArray()
        {
            var doc = new MutableDocument("doc1");
            var addresses = new MutableArrayObject();
            doc.SetArray("addresses", addresses);

            var address1 = new MutableDictionaryObject();
            address1.SetString("street", "1 Main street")
                .SetString("city", "Mountain View")
                .SetString("state", "CA");
            addresses.AddDictionary(address1);

            var address2 = new MutableDictionaryObject();
            address2.SetString("street", "1 Second street")
                .SetString("city", "Palo Alto")
                .SetString("state", "CA");
            addresses.AddDictionary(address2);

            SaveDocument(doc, d =>
            {
                var result = new Dictionary<string, object>
                {
                    ["addresses"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["street"] = "1 Main street",
                            ["city"] = "Mountain View",
                            ["state"] = "CA"
                        },
                        new Dictionary<string, object>
                        {
                            ["street"] = "1 Second street",
                            ["city"] = "Palo Alto",
                            ["state"] = "CA"
                        }
                    }
                };
                d.ToDictionary().ShouldBeEquivalentTo(result);
            });

            address1 = doc.GetArray("addresses").GetDictionary(0);
            address1.SetString("zip", "94042");

            address2 = doc.GetArray("addresses").GetDictionary(1);
            address2.SetString("zip", "94132");

            SaveDocument(doc, d =>
            {
                var result = new Dictionary<string, object> {
                    ["addresses"] = new[] {
                        new Dictionary<string, object> {
                            ["street"] = "1 Main street",
                            ["city"] = "Mountain View",
                            ["state"] = "CA",
                            ["zip"] = "94042"
                        },
                        new Dictionary<string, object> {
                            ["street"] = "1 Second street",
                            ["city"] = "Palo Alto",
                            ["state"] = "CA",
                            ["zip"] = "94132"
                        }
                    }
                };
                d.ToDictionary().ShouldBeEquivalentTo(result);
            });
            
            address1 = doc.GetArray("addresses").GetDictionary(0);
            address1.SetString("street", "2 Main street");

            address2 = doc.GetArray("addresses").GetDictionary(1);
            address2.SetString("street", "2 Second street");

            SaveDocument(doc, d =>
            {
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
                            ["zip"] = "94132"
                        }
                    }
                };
                d.ToDictionary().ShouldBeEquivalentTo(result);
            });
        }

        [Fact]
        public void TestUpdateNestedArray()
        {
            var doc = new MutableDocument("doc1");
            var groups = new MutableArrayObject();
            doc.SetArray("groups", groups);

            var group1 = new MutableArrayObject();
            group1.AddString("a").AddString("b").AddString("c");
            groups.AddArray(group1);

            var group2 = new MutableArrayObject();
            group2.AddInt(1).AddInt(2).AddInt(3);
            groups.AddArray(group2);

            SaveDocument(doc, d =>
            {
                var result = new Dictionary<string, object>
                {
                    ["groups"] = new List<object>
                    {
                        new List<object>
                            { "a", "b", "c" },
                        new List<object>
                            { 1, 2, 3 }
                    }
                };
                d.ToDictionary().ShouldBeEquivalentTo(result);
            });

            group1 = doc.GetArray("groups").GetArray(0);
            group1.SetString(0, "d");
            group1.SetString(1, "e");
            group1.SetString(2, "f");

            group2 = doc.GetArray("groups").GetArray(1);
            group2.SetInt(0, 4);
            group2.SetInt(1, 5);
            group2.SetInt(2, 6);

            SaveDocument(doc, d =>
            {
                var result = new Dictionary<string, object>
                {
                    ["groups"] = new List<object>
                    {
                        new List<object>
                            { "d", "e", "f" },
                        new List<object>
                            { 4, 5, 6 }
                    }
                };
                d.ToDictionary().ShouldBeEquivalentTo(result);
            });
        }

        [Fact]
        public void TestUpdateArrayInDictionary()
        {
            var doc = new MutableDocument("doc1");
            var group1 = new MutableDictionaryObject();
            var member1 = new MutableArrayObject();
            member1.AddString("a").AddString("b").AddString("c");
            group1.SetArray("member", member1);
            doc.SetDictionary("group1", group1);

            var group2 = new MutableDictionaryObject();
            var member2 = new MutableArrayObject();
            member2.AddInt(1).AddInt(2).AddInt(3);
            group2.SetArray("member", member2);
            doc.SetDictionary("group2", group2);

            SaveDocument(doc, d =>
            {
                var result = new Dictionary<string, object>
                {
                    ["group1"] = new Dictionary<string, object>
                    {
                        ["member"] = new List<object>
                        {
                            "a", "b", "c"
                        }
                    },
                    ["group2"] = new Dictionary<string, object>
                    {
                        ["member"] = new List<object>
                        {
                            1, 2, 3
                        }
                    }
                };
                d.ToDictionary().ShouldBeEquivalentTo(result);
            });

            member1 = doc.GetDictionary("group1").GetArray("member");
            member1.SetString(0, "d");
            member1.SetString(1, "e");
            member1.SetString(2, "f");

            member2 = doc.GetDictionary("group2").GetArray("member");
            member2.SetInt(0, 4);
            member2.SetInt(1, 5);
            member2.SetInt(2, 6);

            SaveDocument(doc, d =>
            {
                var result = new Dictionary<string, object>
                {
                    ["group1"] = new Dictionary<string, object>
                    {
                        ["member"] = new List<object>
                        {
                            "d", "e", "f"
                        }
                    },
                    ["group2"] = new Dictionary<string, object>
                    {
                        ["member"] = new List<object>
                        {
                            4, 5, 6
                        }
                    }
                };
                d.ToDictionary().ShouldBeEquivalentTo(result);
            });
        }

        [Fact]
        public void TestSetDictionaryToMultipleKeys()
        {
            var doc = new MutableDocument("doc1");
            var address = new MutableDictionaryObject();
            address.SetString("street", "1 Main street")
                .SetString("city", "Mountain View")
                .SetString("state", "CA");
            doc.SetDictionary("shipping", address);
            doc.SetDictionary("billing", address);

            doc.GetValue("shipping").Should().BeSameAs(address, "because that is the object that was stored");
            doc.GetValue("billing").Should().BeSameAs(address, "because that is the object that was stored");
            
            address.SetString("zip", "94042");
            doc.GetDictionary("shipping")
                .GetString("zip")
                .Should()
                .Be("94042", "because the update should be received by both dictionaries");
            doc.GetDictionary("billing")
                .GetString("zip")
                .Should()
                .Be("94042", "because the update should be received by both dictionaries");

            SaveDocument(doc);

            var shipping = doc.GetDictionary("shipping");
            var billing = doc.GetDictionary("billing");

            shipping.Should().NotBeSameAs(address, "because the dictionaries should now be independent");
            billing.Should().NotBeSameAs(address, "because the dictionaries should now be independent");
            shipping.Should().NotBeSameAs(billing, "because the dictionaries should now be independent");

            shipping.SetString("street", "2 Main street");
            billing.SetString("street", "3 Main street");

            SaveDocument(doc);
            var savedDoc = Db.GetDocument(doc.Id);

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
        public void TestSetArrayToMultipleKeys()
        {
            var doc = new MutableDocument("doc1");
            var phones = new MutableArrayObject();
            phones.AddString("650-000-0001").AddString("650-000-0002");

            doc.SetArray("mobile", phones);
            doc.SetArray("home", phones);

            phones.AddString("650-000-0003");
            doc.GetArray("mobile")
                .Should()
                .ContainInOrder(new[] {"650-000-0001", "650-000-0002", "650-000-0003"},
                    "because both arrays should receive the update");
            doc.GetArray("home")
                .Should()
                .ContainInOrder(new[] { "650-000-0001", "650-000-0002", "650-000-0003" },
                    "because both arrays should receive the update");

            SaveDocument(doc);

            var mobile = doc.GetArray("mobile");
            var home = doc.GetArray("home");
            mobile.Should().NotBeSameAs(phones, "because after save the arrays should be independent");
            home.Should().NotBeSameAs(phones, "because after save the arrays should be independent");
            mobile.Should().NotBeSameAs(home, "because after save the arrays should be independent");

            mobile.AddString("650-000-1234");
            home.AddString("650-000-5678");

            SaveDocument(doc);
            var savedDoc = Db.GetDocument(doc.Id);

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

            SaveDocument(doc, d =>
            {
                d.Count.Should().Be(11, "because that is the number of entries that were added");
                d.Count.Should()
                    .Be(doc.ToDictionary().Count, "because the count should not change when converting to dictionary");
            });
        }

        [Fact]
        public void TestRemoveKeys()
        {
            var doc = new MutableDocument("doc1");
            doc.SetData(new Dictionary<string, object> {
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

            doc.GetValue("name").Should().BeNull("because it was removed");
            doc.GetValue("weight").Should().BeNull("because it was removed");
            doc.GetValue("age").Should().BeNull("because it was removed");
            doc.GetValue("active").Should().BeNull("because it was removed");
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
            doc.GetValue("type").Should().BeNull("because it was removed");
            doc.GetValue("address").Should().BeNull("because it was removed");
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
            existingDoc.SetData(newProps);
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
            doc.SetData(new Dictionary<string, object> {
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
        public void TestDeleteNewDocument()
        {
            var doc = new MutableDocument("doc1");
            doc.SetString("name", "Scott Tiger");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");

            Db.Invoking(d => d.Delete(doc))
                .ShouldThrow<CouchbaseLiteException>()
                .Where(
                    e => e.Error == CouchbaseLiteError.InvalidParameter &&
                         e.Domain == CouchbaseLiteErrorType.CouchbaseLite, "because deleting a non-existent document is invalid");
            doc.IsDeleted.Should().BeFalse("beacuse the document is still not deleted");
            doc.GetString("name").Should().Be("Scott Tiger", "because the delete was invalid");
        }

        [Fact]
        public void TestDeleteDocument()
        {
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott Tiger");
            SaveDocument(doc1);
            
            Db.Delete(doc1);
            Db.GetDocument(doc1.Id).Should().BeNull();
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
            SaveDocument(doc);

            using (var savedDoc = Db.GetDocument(doc.Id)) {
                var address = savedDoc.GetDictionary("address");
                address.GetString("street").Should().Be("1 Main street", "because that is the street that was stored");
                address.GetString("city").Should().Be("Mountain View", "because that is the city that was stored");
                address.GetString("state").Should().Be("CA", "because that is the state that was stored");

                Db.Delete(savedDoc);

                address.GetString("street").Should().Be("1 Main street", "because the dictionary is independent");
                address.GetString("city").Should().Be("Mountain View", "because the dictionary is independent");
                address.GetString("state").Should().Be("CA", "because the dictionary is independent");
            }
        }

        [Fact]
        public void TestArrayAfterDeleteDocument()
        {
            var dict = new Dictionary<string, object> {
                ["members"] = new[] {"a", "b", "c"}
            };

            var doc = new MutableDocument("doc1", dict);
            SaveDocument(doc);

            using (var savedDoc = Db.GetDocument(doc.Id)) {
                var members = savedDoc.GetArray("members");
                members.Count.Should().Be(3, "because three elements were added");
                members.ShouldBeEquivalentTo(dict["members"], "because otherwise the array has incorrect elements");

                Db.Delete(savedDoc);

                members.Count.Should().Be(3, "because the array is independent of the document");
                members.ShouldBeEquivalentTo(dict["members"], "because the array is independent of the document");
            }
        }

        [Fact]
        public void TestPurgeDocument()
        {
            var doc = new MutableDocument("doc1");
            doc.SetString("type", "profile");
            doc.SetString("name", "Scott");
            doc.IsDeleted.Should().BeFalse("beacuse the document is not deleted");
            
            Db.Invoking(db => db.Purge(doc)).ShouldThrow<CouchbaseLiteException>().Where(e =>
                e.Error == CouchbaseLiteError.NotFound && e.Domain == CouchbaseLiteErrorType.CouchbaseLite);

            // Save:
            SaveDocument(doc);

            // Purge should not throw:
            Db.Purge(doc);
        }

        [Fact]
        public void TestReopenDB()
        {
            var doc = new MutableDocument("doc1");
            doc.SetString("string", "str");
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
            doc.SetBlob("data", data);
            doc.SetString("name", "Jim");
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
            doc.SetBlob("data", data);
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
            doc.SetBlob("data", data);
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
            doc.SetBlob("data", data);
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
            doc.SetBlob("data", data);
            doc.SetString("name", "Jim");
            Db.Save(doc);

            ReopenDB();

            var gotDoc = Db.GetDocument("doc1");
            gotDoc.GetBlob("data").Content.Should().Equal(content, "because the data should have been retrieved correctly");

            ReopenDB();

            doc = Db.GetDocument("doc1").ToMutable();
            doc.SetString("foo", "bar");
            Db.Save(doc);
            doc.GetBlob("data").Content.Should().Equal(content, "because the data should have been retrieved correctly");
        }

        [Fact]
        public void TestEnumeratingDocument()
        {
            var doc = new MutableDocument("doc1");
            for (int i = 0; i < 20; i++)
            {
                doc.SetInt($"key{i}", i);
            }

            var content = doc.ToDictionary();
            var result = new Dictionary<string, object>();
            foreach (var item in doc)
            {
                result[item.Key] = item.Value;
            }

            result.ShouldBeEquivalentTo(content, "because that is the correct content");
            content = doc.Remove("key2").SetInt("key20", 20).SetInt("key21", 21).ToDictionary();

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

        [Fact]
        public void TestToMutable()
        {
            var content = Encoding.UTF8.GetBytes(Blob);
            var data = new Blob("text/plain", content);
            using (var mDoc1 = new MutableDocument("doc1")) {
                mDoc1.SetBlob("data", data);
                mDoc1.SetString("name", "Jim");
                mDoc1.SetInt("score", 10);
                using (var mDoc2 = mDoc1.ToMutable()) {
                    mDoc2.Should().NotBeSameAs(mDoc1);
                    mDoc2.GetBlob("data").Should().Be(mDoc1.GetBlob("data"));
                    mDoc2.GetString("name").Should().Be(mDoc1.GetString("name"));
                    mDoc2.GetInt("score").Should().Be(mDoc1.GetInt("score"));
                }

                SaveDocument(mDoc1);
                using (var doc1 = Db.GetDocument(mDoc1.Id)) 
                using (var mDoc3 = doc1.ToMutable()) {
                    doc1.GetBlob("data").Should().Be(mDoc3.GetBlob("data"));
                    doc1.GetString("name").Should().Be(mDoc3.GetString("name"));
                    doc1.GetInt("score").Should().Be(mDoc3.GetInt("score"));
                }
            }
        }

        [Fact]
        public void TestEquality()
        {
            var data1 = Encoding.UTF8.GetBytes("data1");
            var data2 = Encoding.UTF8.GetBytes("data2");

            using (var doc1a = new MutableDocument("doc1"))
            using (var doc1b = new MutableDocument("doc1"))
            using (var doc1c = new MutableDocument("doc1")) {
                doc1a.SetInt("answer", 42);
                doc1a.SetValue("options", new[] { 1, 2, 3 });
                doc1a.SetBlob("attachment", new Blob("text/plain", data1));

                doc1b.SetInt("answer", 42);
                doc1b.SetValue("options", new[] { 1, 2, 3 });
                doc1b.SetBlob("attachment", new Blob("text/plain", data1));

                doc1c.SetInt("answer", 41);
                doc1c.SetValue("options", new[] { 1, 2 });
                doc1c.SetBlob("attachment", new Blob("text/plain", data2));
                doc1c.SetString("comment", "This is a comment");

                doc1a.As<object>().Should().Be(doc1a);
                doc1a.As<object>().Should().Be(doc1b);
                doc1a.As<object>().Should().NotBe(doc1c);

                doc1b.As<object>().Should().Be(doc1a);
                doc1b.As<object>().Should().Be(doc1b);
                doc1b.As<object>().Should().NotBe(doc1c);

                doc1c.As<object>().Should().NotBe(doc1a);
                doc1c.As<object>().Should().NotBe(doc1b);
                doc1c.As<object>().Should().Be(doc1c);

                Db.Save(doc1c);
                using(var savedDoc = Db.GetDocument(doc1c.Id))
                using (var mDoc = savedDoc.ToMutable()) {
                    mDoc.As<object>().Should().Be(savedDoc);
                    mDoc.SetInt("answer", 50);
                    mDoc.As<object>().Should().NotBe(savedDoc);
                }
            }
        }

        [Fact]
        public void TestEqualityDifferentDocID()
        {
            using(var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetInt("answer", 42);
                doc2.SetInt("answer", 42);
                Db.Save(doc1);
                Db.Save(doc2);
                using (var sDoc1 = Db.GetDocument(doc1.Id))
                using (var sDoc2 = Db.GetDocument(doc2.Id)) {
                    sDoc1.As<object>().Should().Be(doc1);
                    sDoc2.As<object>().Should().Be(doc2);

                    doc1.As<object>().Should().Be(doc1);
                    doc1.As<object>().Should().NotBe(doc2);

                    doc2.As<object>().Should().NotBe(doc1);
                    doc2.As<object>().Should().Be(doc2);

                    sDoc1.As<object>().Should().NotBe(sDoc2);
                    sDoc2.As<object>().Should().NotBe(sDoc1);
                }
                
            }
        }

        [Fact]
        public void TestEqualityDifferentDB()
        {
            using (var otherDB = OpenDB("other")) {
                using (var doc1a = new MutableDocument("doc1"))
                using (var doc1b = new MutableDocument("doc1")) {
                    doc1a.SetInt("answer", 42);
                    doc1b.SetInt("answer", 42);
                    doc1a.As<object>().Should().Be(doc1b);

                    Db.Save(doc1a);
                    otherDB.Save(doc1b);
                    using(var sdoc1a = Db.GetDocument(doc1a.Id))
                    using (var sdoc1b = otherDB.GetDocument(doc1b.Id)) {
                        sdoc1a.As<object>().Should().Be(doc1a);
                        sdoc1b.As<object>().Should().Be(doc1b);
                        doc1a.As<object>().Should().NotBe(doc1b);
                        sdoc1a.As<object>().Should().NotBe(sdoc1b);
                    }
                }

                using(var sdoc1a = Db.GetDocument("doc1"))
                using (var sdoc1b = otherDB.GetDocument("doc1")) {
                    sdoc1a.As<object>().Should().NotBe(sdoc1b);

                    using (var sameDB = new Database(Db))
                    using(var anotherDoc1a = sameDB.GetDocument("doc1")) {
                        sdoc1a.As<object>().Should().Be(anotherDoc1a);
                    }
                }
            }
        }

        [ForIssue("couchbase-lite-android/1449")]
        [Fact]
        public void TestDeleteDocAndGetDoc()
        {
            const string docID = "doc-1";
            Db.GetDocument(docID).Should().BeNull();
            using (var mDoc = new MutableDocument(docID)) {
                mDoc.SetString("key", "value");
                Db.Save(mDoc);
                using (var doc = Db.GetDocument(mDoc.Id)) {
                    doc.Should().NotBeNull();
                    Db.Count.Should().Be(1);
                }

                using (var doc = Db.GetDocument(docID)) {
                    doc.Should().NotBeNull();
                    doc.GetString("key").Should().Be("value");
                    Db.Delete(doc);
                    Db.Count.Should().Be(0);
                }

                Db.GetDocument(docID).Should().BeNull();
            }

            using (var mDoc = new MutableDocument(docID)) {
                mDoc.SetString("key", "value");
                Db.Save(mDoc);
                using (var doc = Db.GetDocument(mDoc.Id)) {
                    doc.Should().NotBeNull();
                    Db.Count.Should().Be(1);
                }

                using (var doc = Db.GetDocument(docID)) {
                    doc.Should().NotBeNull();
                    doc.GetString("key").Should().Be("value");
                    Db.Delete(doc);
                    Db.Count.Should().Be(0);
                }

                Db.GetDocument(docID).Should().BeNull();
            }
        }

        private void PopulateData(MutableDocument doc)
        {
            var date = DateTimeOffset.Now;
            doc.SetBoolean("true", true)
                .SetBoolean("false", false)
                .SetString("string", "string")
                .SetInt("zero", 0)
                .SetInt("one", 1)
                .SetInt("minus_one", -1)
                .SetDouble("one_dot_one", 1.1)
                .SetDate("date", date);

            var dict = new MutableDictionaryObject();
            dict.SetString("street", "1 Main street")
                .SetString("city", "Mountain View")
                .SetString("state", "CA");
            doc.SetDictionary("dict", dict);

            var array = new MutableArrayObject();
            array.AddString("650-123-0001");
            array.AddString("650-123-0002");
            doc.SetArray("array", array);

            var content = Encoding.UTF8.GetBytes("12345");
            var blob = new Blob("text/plain", content);
            doc.SetBlob("blob", blob);
        }
    }
}

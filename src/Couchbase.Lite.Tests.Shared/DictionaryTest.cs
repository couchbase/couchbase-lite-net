//
//  DictionaryTest.cs
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
using System.Text;
using Couchbase.Lite;
using FluentAssertions;
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
    public sealed class DictionaryTest : TestCase
    {
#if !WINDOWS_UWP
        public DictionaryTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestCreateDictionary()
        {
            var address = new DictionaryObject();
            address.Count.Should().Be(0, "because the dictionary is empty");
            address.ToDictionary().Should().BeEmpty("because the dictionary is empty");

            var doc1 = new Document("doc1");
            doc1.Set("address", address);
            doc1.GetDictionary("address")
                .Should()
                .BeSameAs(address, "because the document should return the same instance");

            Db.Save(doc1);
            doc1 = Db.GetDocument("doc1");
            doc1.GetDictionary("address").ToDictionary().Should().BeEmpty("because the content should not have changed");
        }

        [Fact]
        public void TestCreateDictionaryWithCSharpDictionary()
        {
            var dict = new Dictionary<string, object> {
                ["street"] = "1 Main street",
                ["city"] = "Mountain View",
                ["state"] = "CA"
            };
            var address = new DictionaryObject(dict);
            address.ShouldBeEquivalentTo(dict, "because that is what was stored");
            address.ToDictionary().ShouldBeEquivalentTo(dict, "because that is what was stored");

            var doc1 = new Document("doc1");
            doc1.Set("address", address);
            doc1.GetDictionary("address")
                .Should()
                .BeSameAs(address, "because the document should return the same instance");

            Db.Save(doc1);
            doc1 = Db.GetDocument("doc1");
            doc1.GetDictionary("address")
                .ToDictionary()
                .ShouldBeEquivalentTo(dict, "because the content should not have changed");
        }

        [Fact]
        public void TestGetValueFromNewEmptyDictionary()
        {
            IDictionaryObject dict = new DictionaryObject();
            dict.GetInt("key").Should().Be(0, "because that is the default value");
            dict.GetLong("key").Should().Be(0L, "because that is the default value");
            dict.GetDouble("key").Should().Be(0.0, "because that is the default value");
            dict.GetBoolean("key").Should().Be(false, "because that is the default value");
            dict.GetDate("key").Should().Be(DateTimeOffset.MinValue, "because that is the default value");
            dict.GetBlob("key").Should().BeNull("because that is the default value");
            dict.GetObject("key").Should().BeNull("because that is the default value");
            dict.GetString("key").Should().BeNull("because that is the default value");
            dict.GetDictionary("key").Should().BeNull("because that is the default value");
            dict.GetArray("key").Should().BeNull("because that is the default value");
            dict.ToDictionary().Should().BeEmpty("because the dictionary is empty");

            var doc = new Document("doc1");
            doc.Set("dict", dict);

            Db.Save(doc);
            doc = Db.GetDocument("doc1");
            dict = doc.GetDictionary("dict");
            dict.GetInt("key").Should().Be(0, "because that is the default value");
            dict.GetLong("key").Should().Be(0L, "because that is the default value");
            dict.GetDouble("key").Should().Be(0.0, "because that is the default value");
            dict.GetBoolean("key").Should().Be(false, "because that is the default value");
            dict.GetDate("key").Should().Be(DateTimeOffset.MinValue, "because that is the default value");
            dict.GetBlob("key").Should().BeNull("because that is the default value");
            dict.GetObject("key").Should().BeNull("because that is the default value");
            dict.GetString("key").Should().BeNull("because that is the default value");
            dict.GetDictionary("key").Should().BeNull("because that is the default value");
            dict.GetArray("key").Should().BeNull("because that is the default value");
            dict.ToDictionary().Should().BeEmpty("because the dictionary is empty");
        }

        [Fact]
        public void TestSetNestedDictionaries()
        {
            var doc = new Document("doc1");
            IDictionaryObject level1 = new DictionaryObject();
            level1.Set("name", "n1");
            doc.Set("level1", level1);

            IDictionaryObject level2 = new DictionaryObject();
            level2.Set("name", "n2");
            level1.Set("level2", level2);

            IDictionaryObject level3 = new DictionaryObject();
            level3.Set("name", "n3");
            level2.Set("level3", level3);

            doc.GetDictionary("level1").ShouldBeEquivalentTo(level1, "because that is what was inserted");
            level1.GetDictionary("level2").ShouldBeEquivalentTo(level2, "because that is what was inserted");
            level2.GetDictionary("level3").ShouldBeEquivalentTo(level3, "because that is what was inserted");
            var dict = new Dictionary<string, object> {
                ["level1"] = new Dictionary<string, object> {
                    ["name"] = "n1",
                    ["level2"] = new Dictionary<string, object> {
                        ["name"] = "n2",
                        ["level3"] = new Dictionary<string, object> {
                            ["name"] = "n3"
                        }
                    }
                }
            };

            doc.ToDictionary().ShouldBeEquivalentTo(dict, "because otherwise the document's contents are incorrect");

            Db.Save(doc);
            doc = Db.GetDocument("doc1");
            doc.GetDictionary("level1")
                .Should()
                .NotBeSameAs(level1, "because a new document should return a new instance");
            level1 = doc.GetDictionary("level1");
            level2 = level1.GetDictionary("level2");
            level3 = level2.GetDictionary("level3");

            level1.GetDictionary("level2").ShouldBeEquivalentTo(level2, "because otherwise the contents are wrong");
            level2.GetDictionary("level3").ShouldBeEquivalentTo(level3, "because otherwise the contents are wrong");
            doc.ToDictionary().ShouldBeEquivalentTo(dict, "because otherwise the document's contents are incorrect");
        }

        [Fact]
        public void TestDictionaryArray()
        {
            var doc = new Document("doc1");
            var data = new[] {
                new Dictionary<string, object> {
                    ["name"] = "1"
                },
                new Dictionary<string, object> {
                    ["name"] = "2"
                },
                new Dictionary<string, object> {
                    ["name"] = "3"
                },
                new Dictionary<string, object> {
                    ["name"] = "4"
                }
            };

            doc.Set(new Dictionary<string, object> {
                ["dicts"] = data
            });

            var dicts = doc.GetArray("dicts");
            dicts.Count.Should().Be(4, "because that is the number of entries added");

            var d1 = dicts.GetDictionary(0);
            var d2 = dicts.GetDictionary(1);
            var d3 = dicts.GetDictionary(2);
            var d4 = dicts.GetDictionary(3);

            d1.GetString("name").Should().Be("1", "because that is what was stored");
            d2.GetString("name").Should().Be("2", "because that is what was stored");
            d3.GetString("name").Should().Be("3", "because that is what was stored");
            d4.GetString("name").Should().Be("4", "because that is what was stored");

            Db.Save(doc);
            doc = Db.GetDocument("doc1");
            dicts = doc.GetArray("dicts");
            dicts.Count.Should().Be(4, "because that is the number of entries");

            d1 = dicts.GetDictionary(0);
            d2 = dicts.GetDictionary(1);
            d3 = dicts.GetDictionary(2);
            d4 = dicts.GetDictionary(3);
            d1.GetString("name").Should().Be("1", "because that is what was stored");
            d2.GetString("name").Should().Be("2", "because that is what was stored");
            d3.GetString("name").Should().Be("3", "because that is what was stored");
            d4.GetString("name").Should().Be("4", "because that is what was stored");
        }

        [Fact]
        public void TestReplaceDictionary()
        {
            var doc = new Document("doc1");
            var profile1 = new DictionaryObject();
            profile1.Set("name", "Scott Tiger");
            doc.Set("profile", profile1);
            doc.GetDictionary("profile").ShouldBeEquivalentTo(profile1, "because that is what was set");

            IDictionaryObject profile2 = new DictionaryObject();
            profile2.Set("name", "Daniel Tiger");
            doc.Set("profile", profile2);
            doc.GetDictionary("profile").ShouldBeEquivalentTo(profile2, "because that is what was set");

            profile1.Set("age", 20);
            profile1.GetString("name").Should().Be("Scott Tiger", "because profile1 should be detached now");
            profile1.GetInt("age").Should().Be(20, "because profile1 should be detached now");

            profile2.GetString("name").Should().Be("Daniel Tiger", "because profile2 should be unchanged");
            profile2.GetObject("age").Should().BeNull("because profile2 should be unchanged");

            Db.Save(doc);
            doc = Db.GetDocument("doc1");

            doc.GetDictionary("profile")
                .Should()
                .NotBeSameAs(profile2, "because a new document should return a new instance");
            profile2 = doc.GetDictionary("profile");
            profile2.GetString("name").Should().Be("Daniel Tiger", "because that is what was saved");
        }

        [Fact]
        public void TestReplaceDictionaryDifferentType()
        {
            var doc = new Document("doc1");
            var profile1 = new DictionaryObject();
            profile1.Set("name", "Scott Tiger");
            doc.Set("profile", profile1);
            doc.GetDictionary("profile").ShouldBeEquivalentTo(profile1, "because that is what was set");

            doc.Set("profile", "Daniel Tiger");
            doc.GetString("profile").Should().Be("Daniel Tiger", "because that is what was set");

            profile1.Set("age", 20);
            profile1.GetString("name").Should().Be("Scott Tiger", "because profile1 should be detached now");
            profile1.GetInt("age").Should().Be(20, "because profile1 should be detached now");

            doc.GetString("profile").Should().Be("Daniel Tiger", "because profile1 should not affect the new value");

            Db.Save(doc);
            doc = Db.GetDocument("doc1");
            doc.GetString("profile").Should().Be("Daniel Tiger", "because that is what was saved");
        }
    }
}

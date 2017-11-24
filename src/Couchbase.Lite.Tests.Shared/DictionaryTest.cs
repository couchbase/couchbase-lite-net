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
            var address = new MutableDictionary();
            address.Count.Should().Be(0, "because the dictionary is empty");
            address.ToDictionary().Should().BeEmpty("because the dictionary is empty");

            var doc1 = new MutableDocument("doc1");
            doc1.SetDictionary("address", address);
            doc1.GetDictionary("address")
                .Should()
                .BeSameAs(address, "because the document should return the same instance");

            Db.Save(doc1);
            var gotDoc = Db.GetDocument("doc1");
            gotDoc.GetDictionary("address").ToDictionary().Should().BeEmpty("because the content should not have changed");
        }

        [Fact]
        public void TestCreateDictionaryWithCSharpDictionary()
        {
            var dict = new Dictionary<string, object> {
                ["street"] = "1 Main street",
                ["city"] = "Mountain View",
                ["state"] = "CA"
            };
            var address = new MutableDictionary(dict);
            address.ShouldBeEquivalentTo(dict, "because that is what was stored");
            address.ToDictionary().ShouldBeEquivalentTo(dict, "because that is what was stored");

            var doc1 = new MutableDocument("doc1");
            doc1.SetDictionary("address", address);
            doc1.GetDictionary("address")
                .Should()
                .BeSameAs(address, "because the document should return the same instance");

            Db.Save(doc1);
            var gotDoc = Db.GetDocument("doc1");
            gotDoc.GetDictionary("address")
                .ToDictionary()
                .ShouldBeEquivalentTo(dict, "because the content should not have changed");
        }

        [Fact]
        public void TestGetValueFromNewEmptyDictionary()
        {
            DictionaryObject dict = new MutableDictionary();
            dict.GetInt("key").Should().Be(0, "because that is the default value");
            dict.GetLong("key").Should().Be(0L, "because that is the default value");
            dict.GetDouble("key").Should().Be(0.0, "because that is the default value");
            dict.GetBoolean("key").Should().Be(false, "because that is the default value");
            dict.GetDate("key").Should().Be(DateTimeOffset.MinValue, "because that is the default value");
            dict.GetBlob("key").Should().BeNull("because that is the default value");
            dict.GetValue("key").Should().BeNull("because that is the default value");
            dict.GetString("key").Should().BeNull("because that is the default value");
            dict.GetDictionary("key").Should().BeNull("because that is the default value");
            dict.GetArray("key").Should().BeNull("because that is the default value");
            dict.ToDictionary().Should().BeEmpty("because the dictionary is empty");

            var doc = new MutableDocument("doc1");
            doc.SetDictionary("dict", dict);

            Db.Save(doc);
            var gotDoc = Db.GetDocument("doc1");
            dict = gotDoc.GetDictionary("dict");
            dict.GetInt("key").Should().Be(0, "because that is the default value");
            dict.GetLong("key").Should().Be(0L, "because that is the default value");
            dict.GetDouble("key").Should().Be(0.0, "because that is the default value");
            dict.GetBoolean("key").Should().Be(false, "because that is the default value");
            dict.GetDate("key").Should().Be(DateTimeOffset.MinValue, "because that is the default value");
            dict.GetBlob("key").Should().BeNull("because that is the default value");
            dict.GetValue("key").Should().BeNull("because that is the default value");
            dict.GetString("key").Should().BeNull("because that is the default value");
            dict.GetDictionary("key").Should().BeNull("because that is the default value");
            dict.GetArray("key").Should().BeNull("because that is the default value");
            dict.ToDictionary().Should().BeEmpty("because the dictionary is empty");
        }

        [Fact]
        public void TestSetNestedDictionaries()
        {
            var doc = new MutableDocument("doc1");
            var level1 = new MutableDictionary();
            level1.SetString("name", "n1");
            doc.SetDictionary("level1", level1);

            var level2 = new MutableDictionary();
            level2.SetString("name", "n2");
            level1.SetDictionary("level2", level2);

            var level3 = new MutableDictionary();
            level3.SetString("name", "n3");
            level2.SetDictionary("level3", level3);

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
            var gotDoc = Db.GetDocument("doc1");
            gotDoc.GetDictionary("level1").Should().NotBeSameAs(level1);
            gotDoc.ToDictionary().ShouldBeEquivalentTo(dict);
        }

        [Fact]
        public void TestDictionaryArray()
        {
            var doc = new MutableDocument("doc1");
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
            var gotDoc = Db.GetDocument("doc1");
            var savedDicts = gotDoc.GetArray("dicts");
            savedDicts.Count.Should().Be(4, "because that is the number of entries");

            var savedD1 = savedDicts.GetDictionary(0);
            var savedD2 = savedDicts.GetDictionary(1);
            var savedD3 = savedDicts.GetDictionary(2);
            var savedD4 = savedDicts.GetDictionary(3);
            savedD1.GetString("name").Should().Be("1", "because that is what was stored");
            savedD2.GetString("name").Should().Be("2", "because that is what was stored");
            savedD3.GetString("name").Should().Be("3", "because that is what was stored");
            savedD4.GetString("name").Should().Be("4", "because that is what was stored");
        }

        [Fact]
        public void TestReplaceDictionary()
        {
            var doc = new MutableDocument("doc1");
            var profile1 = new MutableDictionary();
            profile1.SetString("name", "Scott Tiger");
            doc.SetDictionary("profile", profile1);
            doc.GetDictionary("profile").ShouldBeEquivalentTo(profile1, "because that is what was set");

            var profile2 = new MutableDictionary();
            profile2.SetString("name", "Daniel Tiger");
            doc.SetDictionary("profile", profile2);
            doc.GetDictionary("profile").ShouldBeEquivalentTo(profile2, "because that is what was set");

            profile1.SetInt("age", 20);
            profile1.GetString("name").Should().Be("Scott Tiger", "because profile1 should be detached now");
            profile1.GetInt("age").Should().Be(20, "because profile1 should be detached now");

            profile2.GetString("name").Should().Be("Daniel Tiger", "because profile2 should be unchanged");
            profile2.GetValue("age").Should().BeNull("because profile2 should be unchanged");

            Db.Save(doc);
            var gotDoc = Db.GetDocument("doc1");

            gotDoc.GetDictionary("profile")
                .Should()
                .NotBeSameAs(profile2, "because a new MutableDocument should return a new instance");
            var savedProfile2 = gotDoc.GetDictionary("profile");
            savedProfile2.GetString("name").Should().Be("Daniel Tiger", "because that is what was saved");
        }

        [Fact]
        public void TestReplaceDictionaryDifferentType()
        {
            var doc = new MutableDocument("doc1");
            var profile1 = new MutableDictionary();
            profile1.SetString("name", "Scott Tiger");
            doc.SetDictionary("profile", profile1);
            doc.GetDictionary("profile").ShouldBeEquivalentTo(profile1, "because that is what was set");

            doc.SetString("profile", "Daniel Tiger");
            doc.GetString("profile").Should().Be("Daniel Tiger", "because that is what was set");

            profile1.SetInt("age", 20);
            profile1.GetString("name").Should().Be("Scott Tiger", "because profile1 should be detached now");
            profile1.GetInt("age").Should().Be(20, "because profile1 should be detached now");

            doc.GetString("profile").Should().Be("Daniel Tiger", "because profile1 should not affect the new value");

            Db.Save(doc);
            var gotDoc = Db.GetDocument("doc1");
            gotDoc.GetString("profile").Should().Be("Daniel Tiger", "because that is what was saved");
        }

        [Fact]
        public void TestRemoveDictionary()
        {
            var doc = new MutableDocument("doc1");
            var profile1 = new MutableDictionary();
            profile1.SetString("name", "Scott Tiger");
            doc.SetDictionary("profile", profile1);
            doc.GetDictionary("profile").ShouldBeEquivalentTo(profile1, "because that was what was inserted");
            doc.Contains("profile").Should().BeTrue("because a value exists for that key");

            doc.Remove("profile");
            doc.GetValue("profile").Should().BeNull("beacuse the value for 'profile' was removed");
            doc.Contains("profile").Should().BeFalse("because the value was removed");

            profile1.SetInt("age", 20);
            profile1.GetString("name").Should().Be("Scott Tiger", "because the dictionary object should be unaffected");
            profile1.GetInt("age").Should().Be(20, "because the dictionary should still be editable");

            doc.GetValue("profile").Should()
                .BeNull("because changes to the dictionary should not have any affect anymore");

            var savedDoc = Db.Save(doc);

            savedDoc.GetValue("profile").Should().BeNull("beacuse the value for 'profile' was removed");
            savedDoc.Contains("profile").Should().BeFalse("because the value was removed");
        }

        [Fact]
        public void TestEnumeratingDictionary()
        {
            var dict = new MutableDictionary();
            for (int i = 0; i < 20; i++) {
                dict.SetInt($"key{i}", i);
            }

            var content = dict.ToDictionary();
            var result = new Dictionary<string, object>();
            foreach (var item in dict) {
                result[item.Key] = item.Value;
            }

            result.ShouldBeEquivalentTo(content, "because that is the correct content");
            content = dict.Remove("key2").SetInt("key20", 20).SetInt("key21", 21).ToDictionary();

            result = new Dictionary<string, object>();
            foreach (var item in dict) {
                result[item.Key] = item.Value;
            }

            result.ShouldBeEquivalentTo(content, "because that is the correct content");

            var doc = new MutableDocument("doc1");
            doc.SetDictionary("dict", dict);
            SaveDocument(doc, d =>
            {
                result = new Dictionary<string, object>();
                var dictObj = d.GetDictionary("dict");
                foreach (var item in dictObj)
                {
                    result[item.Key] = item.Value;
                }

                result.ShouldBeEquivalentTo(content, "because that is the correct content");
            });
        }
    }
}

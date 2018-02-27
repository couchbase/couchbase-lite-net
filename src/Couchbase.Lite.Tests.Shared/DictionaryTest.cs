//
//  DictionaryTest.cs
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
            var address = new MutableDictionaryObject();
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
            var address = new MutableDictionaryObject(dict);
            address.ShouldBeEquivalentTo(dict, "because that is what was stored");
            address.ToDictionary().ShouldBeEquivalentTo(dict, "because that is what was stored");

            var doc1 = new MutableDocument("doc1");
            doc1.SetDictionary("address", address);
            doc1.GetDictionary("address")
                .Should()
                .BeSameAs(address, "because the document should return the same instance");

            Db.Save(doc1);
            var gotDoc = Db.GetDocument("doc1");
            gotDoc.Should().NotBeNull();
            gotDoc.GetDictionary("address")
                .ToDictionary()
                .ShouldBeEquivalentTo(dict, "because the content should not have changed");
        }

        [Fact]
        public void TestGetValueFromNewEmptyDictionary()
        {
            DictionaryObject dict = new MutableDictionaryObject();
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
            var level1 = new MutableDictionaryObject();
            level1.SetString("name", "n1");
            doc.SetDictionary("level1", level1);

            var level2 = new MutableDictionaryObject();
            level2.SetString("name", "n2");
            level1.SetDictionary("level2", level2);

            var level3 = new MutableDictionaryObject();
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
        public void TestSetNull()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mDict = new MutableDictionaryObject();
                mDict.SetValue("obj-null", null);
                mDict.SetString("string-null", null);
                mDict.SetArray("array-null", null);
                mDict.SetDictionary("dict-null", null);
                mDoc.SetDictionary("dict", mDict);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("dict").Should().BeTrue();
                    var d = doc.GetDictionary("dict");
                    d.Should().NotBeNull();
                    d.Count.Should().Be(4);
                    d.Contains("obj-null").Should().BeTrue();
                    d.Contains("string-null").Should().BeTrue();
                    d.Contains("array-null").Should().BeTrue();
                    d.Contains("dict-null").Should().BeTrue();
                    d.GetValue("obj-null").Should().BeNull();;
                    d.GetValue("string-null").Should().BeNull();;
                    d.GetValue("array-null").Should().BeNull();;
                    d.GetValue("dict-null").Should().BeNull();
                });
            }
        }

        [Fact]
        public void TestSetOthers()
        {
            // Uncovered by other tests
            var dict = new MutableDictionaryObject();
            dict.SetFloat("pi", 3.14f);
            dict.SetDouble("better_pi", 3.14159);
            dict.SetBoolean("use_better", true);

            dict.GetFloat("pi").Should().Be(3.14f);
            dict.GetDouble("better_pi").Should().Be(3.14159);
            dict.GetDouble("pi").Should().BeApproximately(3.14, 0.00001);
            dict.GetFloat("better_pi").Should().BeApproximately(3.14159f, 0.0000000001f);
            dict.GetBoolean("use_better").Should().BeTrue();
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

            doc.SetData(new Dictionary<string, object> {
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
            var profile1 = new MutableDictionaryObject();
            profile1.SetString("name", "Scott Tiger");
            doc.SetDictionary("profile", profile1);
            doc.GetDictionary("profile").ShouldBeEquivalentTo(profile1, "because that is what was set");

            var profile2 = new MutableDictionaryObject();
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
            var profile1 = new MutableDictionaryObject();
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
            var profile1 = new MutableDictionaryObject();
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

            SaveDocument(doc, d =>
            {
                d.GetValue("profile").Should().BeNull("beacuse the value for 'profile' was removed");
                d.Contains("profile").Should().BeFalse("because the value was removed");
            });
        }

        [Fact]
        public void TestEnumeratingDictionary()
        {
            var dict = new MutableDictionaryObject();
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

        [ForIssue("couchbase-lite-core/230")]
        [Fact]
        public void TestLargeLongValue()
        {
            using (var doc = new MutableDocument("test")) {
                var num1 = 1234567L;
                var num2 = 12345678L;
                var num3 = 123456789L;
                doc.SetLong("num1", num1);
                doc.SetLong("num2", num2);
                doc.SetLong("num3", num3);
                Db.Save(doc);
                using (var newDoc = Db.GetDocument(doc.Id).ToMutable()) {
                    newDoc.GetLong("num1").Should().Be(num1);
                    newDoc.GetLong("num2").Should().Be(num2);
                    newDoc.GetLong("num3").Should().Be(num3);
                }
            }
        }

        [Fact]
        public void TestLargeLongValue2()
        {
            // https://forums.couchbase.com/t/long-value-on-document-changed-after-saved-to-db/14259
            using (var doc = new MutableDocument("test")) {
                var num1 = 11989091L;
                var num2 = 231548688L;
                doc.SetLong("num1", num1);
                doc.SetLong("num2", num2);
                Db.Save(doc);
                using (var newDoc = Db.GetDocument(doc.Id).ToMutable()) {
                    newDoc.GetLong("num1").Should().Be(num1);
                    newDoc.GetLong("num2").Should().Be(num2);
                }
            }
        }

        [Fact]
        public void TestGetDictionary()
        {
            var mNestedDict = new MutableDictionaryObject();
            mNestedDict.SetLong("key1", 1L);
            mNestedDict.SetString("key2", "Hello");
            mNestedDict.SetValue("key3", null);

            var mDict = new MutableDictionaryObject();
            mDict.SetLong("key1", 1L);
            mDict.SetString("key2", "Hello");
            mDict.SetValue("key3", null);
            mDict.SetDictionary("nestedDict", mNestedDict);

            using (var mDoc = new MutableDocument("test")) {
                mDoc.SetDictionary("dict", mDict);
                
                Db.Save(mDoc);
                using (var doc = Db.GetDocument(mDoc.Id).ToMutable()) {
                    var dict = doc.GetDictionary("dict");
                    dict.Should().NotBeNull();
                    dict.GetDictionary("not-exists").Should().BeNull();
                    var nestedDict = dict.GetDictionary("nestedDict");
                    nestedDict.Should().NotBeNull();
                    nestedDict.ToDictionary().ShouldBeEquivalentTo(mNestedDict.ToDictionary());
                }
            }
        }

        [Fact]
        public void TestGetArray()
        {
            var mNestedArray = new MutableArrayObject();
            mNestedArray.AddLong(1L);
            mNestedArray.AddString("Hello");
            mNestedArray.AddValue(null);

            var mArray = new MutableArrayObject();
            mArray.AddLong(1L);
            mArray.AddString("Hello");
            mArray.AddValue(null);
            mArray.AddArray(mNestedArray);

            using (var mDoc = new MutableDocument("test")) {
                mDoc.SetArray("array", mArray);
                
                Db.Save(mDoc);
                using (var doc = Db.GetDocument(mDoc.Id).ToMutable()) {
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.GetArray(0).Should().BeNull();
                    array.GetArray(1).Should().BeNull();
                    array.GetArray(2).Should().BeNull();
                    array.GetArray(3).Should().NotBeNull();

                    var nestedArray = array.GetArray(3);
                    nestedArray.ShouldBeEquivalentTo(mNestedArray);
                    array.ShouldBeEquivalentTo(mArray);
                }
            }
        }
    }
}

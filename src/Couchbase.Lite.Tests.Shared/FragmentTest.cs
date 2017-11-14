//
//  FragmentTest.cs
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
using System.Collections;
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
    public sealed class FragmentTest : TestCase
    {
#if !WINDOWS_UWP
        public FragmentTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestGetDocFragmentWithID()
        {
            var dict = new Dictionary<string, object> {
                ["address"] = new Dictionary<string, object> { 
                    ["street"] = "1 Main street",
                    ["city"] = "Mountain View",
                    ["state"] = "CA"
                }
            };

            Db.Save(new MutableDocument("doc1", dict));

            var doc = Db["doc1"];
            doc.Should().NotBeNull("because the subscript operator should never return null");
            doc.Exists.Should().BeTrue("because the document was saved");
            doc.Document.Should().NotBeNull("because the document exists");
            doc["address"]["street"].ToString().Should().Be("1 Main street", "because that is what was stored");
            doc["address"]["city"].ToString().Should().Be("Mountain View", "because that is what was stored");
            doc["address"]["state"].ToString().Should().Be("CA", "because that is what was stored");
        }

        [Fact]
        public void TestGetDocFragmentWithNonExistingID()
        {
            var doc = Db["doc1"];
            doc.Should().NotBeNull("because the subscript operator should never return null");
            doc.Exists.Should().BeFalse("because the document was never created");
            doc.Document.Should().BeNull("because the document does not exist");
            doc["address"]["street"].ToString().Should().BeNull("because the document does not exist");
            doc["address"]["city"].ToString().Should().BeNull("because the document does not exist");
            doc["address"]["state"].ToString().Should().BeNull("because the document does not exist");
        }

        [Fact]
        public void TestGetFragmentFromDictionaryValue()
        {
            var dict = new Dictionary<string, object> {
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Main street",
                    ["city"] = "Mountain View",
                    ["state"] = "CA"
                }
            };

            var doc = new MutableDocument("doc1", dict);
            SaveDocument(doc, d =>
            {
                var fragment = d["address"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().BeNull("because this fragment is not of this type");
                fragment.ToArray().Should().BeNull("because this fragment is not of this type");
                fragment.ToInt().Should().Be(0, "because that is the default value");
                fragment.ToLong().Should().Be(0L, "because that is the default value");
                fragment.ToDouble().Should().Be(0.0, "because that is the default value");
                fragment.ToFloat().Should().Be(0.0f, "because that is the default value");
                fragment.ToBoolean().Should().Be(true, "because that is the non-zero value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
                fragment.ToDictionary().Should().NotBeNull("because this fragment is of this type");
                fragment.ToObject()
                    .Should()
                    .BeSameAs(fragment.ToDictionary())
                    .And
                    .BeSameAs(fragment.Value, "because all three of these should access the same object");
                fragment.ToDictionary()
                    .ToDictionary()
                    .ShouldBeEquivalentTo(dict["address"], "because otherwise the contents are incorrect");
            });
        }

        [Fact]
        public void TestGetFragmentFromArrayValue()
        {
            var references =  new[] {
                new Dictionary<string, object> {
                    ["name"] = "Scott"
                },
                new Dictionary<string, object> {
                    ["name"] = "Sam"
                }
            };

            var dict = new Dictionary<string, object> {
                ["references"] = references
            };

            var doc = new MutableDocument("doc1", dict);
            SaveDocument(doc, d =>
            {
                var fragment = d["references"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().BeNull("because this fragment is not of this type");
                fragment.ToDictionary().Should().BeNull("because this fragment is not of this type");
                fragment.ToInt().Should().Be(0, "because that is the default value");
                fragment.ToLong().Should().Be(0L, "because that is the default value");
                fragment.ToDouble().Should().Be(0.0, "because that is the default value");
                fragment.ToFloat().Should().Be(0.0f, "because that is the default value");
                fragment.ToBoolean().Should().Be(true, "because that is the non-zero value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
                fragment.ToArray().Should().NotBeNull("because this fragment is of this type");
                fragment.ToObject()
                    .Should()
                    .BeSameAs(fragment.ToArray())
                    .And
                    .BeSameAs(fragment.Value, "because all three of these should access the same object");

                fragment.ToArray().ToList().ShouldAllBeEquivalentTo(references);
            });
        }

        [Fact]
        public void TestGetFragmentFromInteger()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("integer", 10);
            SaveDocument(doc, d =>
            {
                var fragment = d["integer"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().BeNull("because this fragment is not of this type");
                fragment.ToArray().Should().BeNull("because this fragment is not of this type");
                fragment.ToDictionary().Should().BeNull("because this fragment is not of this type");
                fragment.ToInt().Should().Be(10, "because that is the stored value");
                fragment.ToLong().Should().Be(10L, "because that is the converted value");
                fragment.ToDouble().Should().Be(10.0, "because that is the converted value");
                fragment.ToFloat().Should().Be(10.0f, "because that is the converted value");
                fragment.ToBoolean().Should().Be(true, "because that is the converted value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromFloat()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("float", 100.10f);
            SaveDocument(doc, d =>
            {
                var fragment = d["float"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().BeNull("because this fragment is not of this type");
                fragment.ToArray().Should().BeNull("because this fragment is not of this type");
                fragment.ToDictionary().Should().BeNull("because this fragment is not of this type");
                fragment.ToInt().Should().Be(100, "because that is the stored value");
                fragment.ToLong().Should().Be(100L, "because that is the converted value");
                fragment.ToFloat().Should().Be(100.10f, "because that is the stored value");
                fragment.ToDouble().Should().BeApproximately(100.10, 0.0001, "because that is the converted value");
                fragment.ToBoolean().Should().Be(true, "because that is the converted value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromLong()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("long", 10L);
            SaveDocument(doc, d =>
            {
                var fragment = d["long"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().BeNull("because this fragment is not of this type");
                fragment.ToArray().Should().BeNull("because this fragment is not of this type");
                fragment.ToDictionary().Should().BeNull("because this fragment is not of this type");
                fragment.ToInt().Should().Be(10, "because that is the converted value");
                fragment.ToLong().Should().Be(10L, "because that is the stored value");
                fragment.ToDouble().Should().Be(10.0, "because that is the converted value");
                fragment.ToFloat().Should().Be(10.0f, "because that is the converted value");
                fragment.ToBoolean().Should().Be(true, "because that is the converted value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromDouble()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("double", 100.10);
            SaveDocument(doc, d =>
            {
                var fragment = d["double"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().BeNull("because this fragment is not of this type");
                fragment.ToArray().Should().BeNull("because this fragment is not of this type");
                fragment.ToDictionary().Should().BeNull("because this fragment is not of this type");
                fragment.ToInt().Should().Be(100, "because that is the converted value");
                fragment.ToLong().Should().Be(100L, "because that is the converted value");
                fragment.ToDouble().Should().Be(100.10, "because that is the stored value");
                fragment.ToFloat().Should().Be(100.10f, "because that is the default value");
                fragment.ToBoolean().Should().Be(true, "because that is the converted value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromBoolean()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("boolean", true);
            SaveDocument(doc, d =>
            {
                var fragment = d["boolean"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().BeNull("because this fragment is not of this type");
                fragment.ToArray().Should().BeNull("because this fragment is not of this type");
                fragment.ToDictionary().Should().BeNull("because this fragment is not of this type");
                fragment.ToInt().Should().Be(1, "because that is the converted value");
                fragment.ToLong().Should().Be(1L, "because that is the converted value");
                fragment.ToDouble().Should().Be(1.0, "because that is the converted value");
                fragment.ToFloat().Should().Be(1.0f, "because that is the converted value");
                fragment.ToBoolean().Should().Be(true, "because that is the stored value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromDate()
        {
            var date = DateTimeOffset.Now;
            var doc = new MutableDocument("doc1");
            doc.Set("date", date);
            SaveDocument(doc, d =>
            {
                var fragment = d["date"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().NotBeNull("because this fragment is convertible to string");
                fragment.ToArray().Should().BeNull("because this fragment is not of this type");
                fragment.ToDictionary().Should().BeNull("because this fragment is not of this type");
                fragment.ToInt().Should().Be(0, "because that is the default value");
                fragment.ToLong().Should().Be(0L, "because that is the default value");
                fragment.ToDouble().Should().Be(0.0, "because that is the default value");
                fragment.ToFloat().Should().Be(0.0f, "because that is the default value");
                fragment.ToBoolean().Should().Be(true, "because that is the non-zero value");
                fragment.ToDate().Should().Be(date, "because that is the stored value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromString()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("string", "hello world");
            SaveDocument(doc, d =>
            {
                var fragment = d["string"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().Be("hello world", "because that is the stored value");
                fragment.ToArray().Should().BeNull("because this fragment is not of this type");
                fragment.ToDictionary().Should().BeNull("because this fragment is not of this type");
                fragment.ToInt().Should().Be(0, "because that is the default value");
                fragment.ToLong().Should().Be(0L, "because that is the default value");
                fragment.ToDouble().Should().Be(0.0, "because that is the default value");
                fragment.ToFloat().Should().Be(0.0f, "because that is the default value");
                fragment.ToBoolean().Should().Be(true, "because that is the non-zero value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
                fragment.ToObject()
                    .Should()
                    .Be(fragment.Value)
                    .And.Be(fragment.ToString(),
                        "because all three of these accessors should return the same value");
            });
        }

        [Fact]
        public void TestGetNestedDictionaryFragment()
        {
            var phones = new Dictionary<string, object> {
                ["mobile"] = "650-123-4567"
            };

            var dict = new Dictionary<string, object> {
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Main street",
                    ["phones"] = phones
                }
            };

            var doc = new MutableDocument("doc1", dict);
            SaveDocument(doc, d =>
            {
                var fragment = d["address"]["phones"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().BeNull("because this fragment is not of this type");
                fragment.ToArray().Should().BeNull("because this fragment is not of this type");
                fragment.ToInt().Should().Be(0, "because that is the default value");
                fragment.ToLong().Should().Be(0L, "because that is the default value");
                fragment.ToDouble().Should().Be(0.0, "because that is the default value");
                fragment.ToFloat().Should().Be(0.0f, "because that is the default value");
                fragment.ToBoolean().Should().Be(true, "because that is the non-zero value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
                fragment.ToDictionary().Should().NotBeNull("because this fragment is of this type");
                fragment.ToObject()
                    .Should()
                    .BeSameAs(fragment.Value)
                    .And.BeSameAs(fragment.ToDictionary(),
                        "because all three of these accessors should return the same object");
                fragment.ToDictionary().ShouldBeEquivalentTo(phones, "because that is the stored content");
                fragment.ToDictionary().Count.Should().Be(1, "because there is one entry in the dictionary");
            });
        }

        [Fact]
        public void TestGetNestedNonExistingDictionaryFragment()
        {
            var phones = new Dictionary<string, object> {
                ["mobile"] = "650-123-4567"
            };

            var dict = new Dictionary<string, object> {
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Main street",
                    ["phones"] = phones
                }
            };

            var doc = new MutableDocument("doc1", dict);
            SaveDocument(doc, d =>
            {
                var fragment = d["address"]["country"];
                fragment.Exists.Should().BeFalse("because this portion of the data doesn't exist");
                fragment.ToString().Should().BeNull("because that is the default value");
                fragment.ToArray().Should().BeNull("because that is the default value");
                fragment.ToInt().Should().Be(0, "because that is the default value");
                fragment.ToLong().Should().Be(0L, "because that is the default value");
                fragment.ToDouble().Should().Be(0.0, "because that is the default value");
                fragment.ToFloat().Should().Be(0.0f, "because that is the default value");
                fragment.ToBoolean().Should().Be(false, "because that is the default value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().BeNull("because this fragment doesn't have a value");
                fragment.Value.Should().BeNull("because this fragment doesn't have a value");
                fragment.ToDictionary().Should().BeNull("because that is the default value");
            });
        }

        [Fact]
        public void TestGetNestedArrayFragments()
        {
            var nested = new[] {4L, 5L, 6L};
            var dict = new Dictionary<string, object> {
                ["nested-array"] = new object[] {
                    new[] {1, 2, 3},
                    nested
                }
            };

            var doc = new MutableDocument("doc1", dict);
            SaveDocument(doc, d =>
            {
                var fragment = d["nested-array"][1];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.ToString().Should().BeNull("because that is the default value");
                fragment.ToInt().Should().Be(0, "because that is the default value");
                fragment.ToLong().Should().Be(0L, "because that is the default value");
                fragment.ToDouble().Should().Be(0.0, "because that is the default value");
                fragment.ToFloat().Should().Be(0.0f, "because that is the default value");
                fragment.ToBoolean().Should().Be(true, "because that is the default value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().NotBeNull("because this fragment has a value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
                fragment.ToDictionary().Should().BeNull("because that is the default value");
                fragment.ToArray().Should().NotBeNull("because this fragment is of this type");
                fragment.ToObject().As<ArrayObject>()
                    .Should()
                    .ContainInOrder(fragment.ToArray())
                    .And.ContainInOrder(fragment.Value.As<ArrayObject>(),
                        "because all three of these accessors should return the same value");
                fragment.ToArray().Should().ContainInOrder(nested, "because that is what was stored");
                fragment.ToArray().Count.Should().Be(3, "because there are three elements inside");
            });
        }

        [Fact]
        public void TestGetNestedNonExistingArrayFragments()
        {
            var nested = new[] { 1, 2, 3 };
            var dict = new Dictionary<string, object> {
                ["nested-array"] = new object[] {
                    nested,
                    new[] {4, 5, 6}
                }
            };

            var doc = new MutableDocument("doc1", dict);
            SaveDocument(doc, d =>
            {
                var fragment = d["nested-array"][2];
                fragment.Exists.Should().BeFalse("because this portion of the data doesn't exist");
                fragment.ToString().Should().BeNull("because that is the default value");
                fragment.ToArray().Should().BeNull("because that is the default value");
                fragment.ToInt().Should().Be(0, "because that is the default value");
                fragment.ToLong().Should().Be(0L, "because that is the default value");
                fragment.ToDouble().Should().Be(0.0, "because that is the default value");
                fragment.ToFloat().Should().Be(0.0f, "because that is the default value");
                fragment.ToBoolean().Should().Be(false, "because that is the default value");
                fragment.ToDate().Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.ToObject().Should().BeNull("because this fragment doesn't have a value");
                fragment.Value.Should().BeNull("because this fragment doesn't have a value");
                fragment.ToDictionary().Should().BeNull("because that is the default value");
            });
        }

        [Fact]
        public void TestDictionaryFragmentSet()
        {
            var date = DateTimeOffset.Now;
            var doc = new MutableDocument("doc1");
            doc["string"].Value = "value";
            doc["bool"].Value = true;
            doc["int"].Value = 7;
            doc["long"].Value = 8L;
            doc["float"].Value = 2.2f;
            doc["double"].Value = 3.3;
            doc["date"].Value = date;

            SaveDocument(doc, d =>
            {
                d["string"].ToString().Should().Be("value", "because that is what was stored");
                d["bool"].ToBoolean().Should().BeTrue("because that is what was stored");
                d["int"].ToInt().Should().Be(7, "because that is what was stored");
                d["long"].ToLong().Should().Be(8L, "because that is what was stored");
                d["double"].ToDouble().Should().Be(3.3, "because that is what was stored");
                d["float"].ToFloat().Should().Be(2.2f, "because that is what was stored");
                d["date"].ToDate().Should().Be(date, "because that is what was stored");
            });
        }

        [Fact]
        public void TestDictionaryFragmentSetDictionary()
        {
            var doc = new MutableDocument("doc1");
            var dict = new MutableDictionary();
            dict.Set("name", "Jason")
                .Set("address", new Dictionary<string, object> {
                    ["street"] = "1 Main Street",
                    ["phones"] = new Dictionary<string, object> {
                        ["mobile"] = "650-123-4567"
                    }
                });
            doc["dict"].Value = dict;
            SaveDocument(doc, d =>
            {
                d["dict"]["name"].ToString().Should().Be("Jason", "because that is what was stored");
                d["dict"]["address"]["street"]
                    .ToString()
                    .Should()
                    .Be("1 Main Street", "because that is what was stored");
                d["dict"]["address"]["phones"]["mobile"]
                    .ToString()
                    .Should()
                    .Be("650-123-4567", "because that is what was stored");
            });
        }

        [Fact]
        public void TestDicionaryFragmentSetArray()
        {
            var doc = new MutableDocument("doc1");
            var array = new MutableArray() {
                0,
                1,
                2
            };

            doc["array"].Value = array;
            SaveDocument(doc, d =>
            {
                d["array"][-1].Value.Should().BeNull("because that is an invalid index");
                d["array"][-1].Exists.Should().BeFalse("because there is no data at the invalid index");
                d["array"][0].ToInt().Should().Be(0, "because that is what was stored");
                d["array"][1].ToInt().Should().Be(1, "because that is what was stored");
                d["array"][2].ToInt().Should().Be(2, "because that is what was stored");
                d["array"][3].Value.Should().BeNull("because that is an invalid index");
                d["array"][3].Exists.Should().BeFalse("because there is no data at the invalid index");
            });
        }

        [Fact]
        public void TestDictionaryFragmentSetCSharpDictionary()
        {
            var doc = new MutableDocument("doc1");
            doc["dict"].Value = new Dictionary<string, object> {
                ["name"] = "Jason",
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Main Street",
                    ["phones"] = new Dictionary<string, object> {
                        ["mobile"] = "650-123-4567"
                    }
                }
            };

            SaveDocument(doc, d =>
            {
                d["dict"]["name"].ToString().Should().Be("Jason", "because that is what was stored");
                d["dict"]["address"]["street"]
                    .ToString()
                    .Should()
                    .Be("1 Main Street", "because that is what was stored");
                d["dict"]["address"]["phones"]["mobile"]
                    .ToString()
                    .Should()
                    .Be("650-123-4567", "because that is what was stored");
            });
        }

        [Fact]
        public void TestDictionaryFragmentSetCSharpList()
        {
            var doc = new MutableDocument("doc1");
            doc["dict"].Value = new Dictionary<string, object>();
            doc["dict"]["array"].Value = new[] {0, 1, 2};

            SaveDocument(doc, d =>
            {
                d["dict"]["array"][-1].Value.Should().BeNull("because that is an invalid index");
                d["dict"]["array"][-1].Exists.Should().BeFalse("because there is no data at the invalid index");
                d["dict"]["array"][0].ToInt().Should().Be(0, "because that is what was stored");
                d["dict"]["array"][1].ToInt().Should().Be(1, "because that is what was stored");
                d["dict"]["array"][2].ToInt().Should().Be(2, "because that is what was stored");
                d["dict"]["array"][3].Value.Should().BeNull("because that is an invalid index");
                d["dict"]["array"][3].Exists.Should().BeFalse("because there is no data at the invalid index");
            });

            doc.Dispose();
        }

        [Fact]
        public void TestNonDictionaryFragmentSetObject()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("string1", "value1").Set("string2", "value2");
            SaveDocument(doc, d =>
            {
                var md = d.ToMutable();
                md["string1"].Value = 10;
                md["string1"].ToInt().Should().Be(10, "because the value was changed");
                md["string2"].ToString().Should().Be("value2", "because that is what was stored");
                md.Dispose();
            });

            doc.Dispose();
        }

        [Fact]
        public void TestArrayFragmentSet()
        {
            var date = DateTimeOffset.Now;
            var doc = new MutableDocument("doc1");
            doc["array"].Value = new object[] {
                "string",
                10,
                10.10,
                true,
                date
            };

            SaveDocument(doc, d =>
            {
                d["array"][-1].Should().NotBeNull("because the subscript operator should never return null");
                d["array"][-1].Exists.Should().BeFalse("because there is no data at an invalid index");
                for (int i = 0; i < 5; i++) {
                    d["array"][i].Should().NotBeNull("because the subscript operator should never return null");
                    d["array"][i].Exists.Should().BeTrue("because there is data at this index");
                }

                d["array"][5].Should().NotBeNull("because the subscript operator should never return null");
                d["array"][5].Exists.Should().BeFalse("because there is no data at an invalid index");

                d["array"][0].ToString().Should().Be("string", "because that is what was stored");
                d["array"][1].ToInt().Should().Be(10, "because that is what was stored");
                d["array"][2].ToDouble().Should().Be(10.10, "because that is what was stored");
                d["array"][3].ToBoolean().Should().Be(true, "because that is what was stored");
                d["array"][4].ToDate().Should().Be(date, "because that is what was stored");
            });
        }

        [Fact]
        public void TestArrayFragmentSetDictionary()
        {
            var doc = new MutableDocument("doc1");
            var dict = new MutableDictionary();
            dict.Set("name", "Jason")
                .Set("address", new Dictionary<string, object> {
                    ["street"] = "1 Main Street",
                    ["phones"] = new Dictionary<string, object> {
                        ["mobile"] = "650-123-4567"
                    }
                });

            doc["array"].Value = new[] {
                dict
            };

            SaveDocument(doc, d =>
            {
                d["array"][-1].Value.Should().BeNull("because that is an invalid index");
                d["array"][-1].Exists.Should().BeFalse("because there is no data at the invalid index");
                d["array"][0].Exists.Should().BeTrue("because data exists at this index");
                d["array"][1].Value.Should().BeNull("because that is an invalid index");
                d["array"][1].Exists.Should().BeFalse("because there is no data at the invalid index");

                d["array"][0]["name"].ToString().Should().Be("Jason", "because that is what was stored");
                d["array"][0]["address"]["street"]
                    .ToString()
                    .Should()
                    .Be("1 Main Street", "because that is what was stored");
                d["array"][0]["address"]["phones"]["mobile"]
                    .ToString()
                    .Should()
                    .Be("650-123-4567", "because that is what was stored");
            });
        }

        [Fact]
        public void TestArrayFragmentSetCSharpDictionary()
        {
            var doc = new MutableDocument("doc1");
            doc["array"].Value = new List<object>();
            doc["array"]
                .ToArray()
                .Add(new Dictionary<string, object> {
                    ["name"] = "Jason",
                    ["address"] = new Dictionary<string, object> {
                        ["street"] = "1 Main Street",
                        ["phones"] = new Dictionary<string, object> {
                            ["mobile"] = "650-123-4567"
                        }
                    }
                });

            SaveDocument(doc, d =>
            {
                d["array"][-1].Value.Should().BeNull("because that is an invalid index");
                d["array"][-1].Exists.Should().BeFalse("because there is no data at the invalid index");
                d["array"][0].Exists.Should().BeTrue("because data exists at this index");
                d["array"][1].Value.Should().BeNull("because that is an invalid index");
                d["array"][1].Exists.Should().BeFalse("because there is no data at the invalid index");

                d["array"][0]["name"].ToString().Should().Be("Jason", "because that is what was stored");
                d["array"][0]["address"]["street"]
                    .ToString()
                    .Should()
                    .Be("1 Main Street", "because that is what was stored");
                d["array"][0]["address"]["phones"]["mobile"]
                    .ToString()
                    .Should()
                    .Be("650-123-4567", "because that is what was stored");
            });
        }

        [Fact]
        public void TestArrayFragmentSetArrayObject()
        {
            var doc = new MutableDocument("doc1");
            doc["array"].Value = new List<object>();
            var array = new MutableArray {
                "Jason",
                5.5,
                true
            };

            doc["array"].ToArray().Add(array);

            SaveDocument(doc, d =>
            {
                d["array"][0][0].ToString().Should().Be("Jason", "because that is the value that was stored");
                d["array"][0][1].ToDouble().Should().Be(5.5, "because that is the value that was stored");
                d["array"][0][2].ToBoolean().Should().Be(true, "because that is the value that was stored");
            });
        }

        [Fact]
        public void TestArrayFragmentSetArray()
        {
            var doc = new MutableDocument("doc1");
            doc["array"].Value = new List<object>();
            doc["array"].ToArray().Add(new object[] {"Jason", 5.5, true});

            SaveDocument(doc, d =>
            {
                d["array"][0][0].ToString().Should().Be("Jason", "because that is the value that was stored");
                d["array"][0][1].ToDouble().Should().Be(5.5, "because that is the value that was stored");
                d["array"][0][2].ToBoolean().Should().Be(true, "because that is the value that was stored");
            });
        }

        [Fact]
        public void TestNonExistingArrayFragmentSetObject()
        {
            var doc = new MutableDocument("doc1");

            doc.Invoking(d => d["array"][0][0].Value = 1)
                .ShouldThrow<InvalidOperationException>("because the path does not exist");
            doc.Invoking(d => d["array"][0][1].Value = false)
                .ShouldThrow<InvalidOperationException>("because the path does not exist");
            doc.Invoking(d => d["array"][0][2].Value = "hello")
                .ShouldThrow<InvalidOperationException>("because the path does not exist");

            SaveDocument(doc, d =>
            {
                d["array"][0][0].ToInt().Should().Be(0);
                d["array"][0][1].ToBoolean().Should().Be(false);
                d["array"][0][2].ToString().Should().BeNull();
            });
        }

        [Fact]
        public void TestOutOfRangeArrayFragmentSetObject()
        {
            var doc = new MutableDocument("doc1");
            doc["array"].Value = new List<object>();
            doc["array"].ToArray().Add(new object[] { "Jason", 5.5, true });
            doc.Invoking(d => d["array"][0][3].Value = 1).ShouldThrow<InvalidOperationException>();

            SaveDocument(doc, d =>
            {
                d["array"][0][3].Exists.Should().BeFalse();
            });
        }

        [Fact]
        public void TestGetFragmentValues()
        {
            var doc = new MutableDocument("doc1");
            doc.Set(new Dictionary<string, object> {
                ["name"] = "Jason",
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Main Street",
                    ["phones"] = new Dictionary<string, object> {
                        ["mobile"] = "650-123-4567"
                    }
                },
                ["references"] = new[] {
                    new Dictionary<string, object> {
                        ["name"] = "Scott"
                    },
                    new Dictionary<string, object> {
                        ["name"] = "Sam"
                    }
                }
            });

            doc["name"].ToString().Should().Be("Jason", "because that is what was stored");
            doc["address"]["street"].ToString().Should().Be("1 Main Street", "because that is what was stored");
            doc["address"]["phones"]["mobile"].ToString().Should().Be("650-123-4567", "because that is what was stored");
            doc["references"][0]["name"].ToString().Should().Be("Scott", "because that is what was stored");
            doc["references"][1]["name"].ToString().Should().Be("Sam", "because that is what was stored");

            doc["references"][2]["name"].Value.Should().BeNull("because this is an invalid index");
            doc["dummy"]["dummy"]["dummy"].Value.Should().BeNull("because these are invalid keys");
            doc["dummy"]["dummy"][0]["dummy"].Value.Should().BeNull("because these are invalid keys and indices");
        }

        [Fact]
        public void TestSetFragmentValues()
        {
            var doc = new MutableDocument("doc1");
            doc["name"].Value = "Jason";

            doc["address"].Value = new MutableDictionary();
            doc["address"]["street"].Value = "1 Main Street";
            doc["address"]["phones"].Value = new MutableDictionary();
            doc["address"]["phones"]["mobile"].Value = "650-123-4567";

            doc["name"].ToString().Should().Be("Jason", "because that is what was stored");
            doc["address"]["street"].ToString().Should().Be("1 Main Street", "because that is what was stored");
            doc["address"]["phones"]["mobile"].ToString().Should().Be("650-123-4567", "because that is what was stored");
        }
    }
}

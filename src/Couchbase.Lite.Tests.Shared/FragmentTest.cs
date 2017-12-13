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
            doc["address"]["street"].String.Should().Be("1 Main street", "because that is what was stored");
            doc["address"]["city"].String.Should().Be("Mountain View", "because that is what was stored");
            doc["address"]["state"].String.Should().Be("CA", "because that is what was stored");
        }

        [Fact]
        public void TestGetDocFragmentWithNonExistingID()
        {
            var doc = Db["doc1"];
            doc.Should().NotBeNull("because the subscript operator should never return null");
            doc.Exists.Should().BeFalse("because the document was never created");
            doc.Document.Should().BeNull("because the document does not exist");
            doc["address"]["street"].String.Should().BeNull("because the document does not exist");
            doc["address"]["city"].String.Should().BeNull("because the document does not exist");
            doc["address"]["state"].String.Should().BeNull("because the document does not exist");
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
                fragment.String.Should().BeNull("because this fragment is not of this type");
                fragment.Array.Should().BeNull("because this fragment is not of this type");
                fragment.Int.Should().Be(0, "because that is the default value");
                fragment.Long.Should().Be(0L, "because that is the default value");
                fragment.Double.Should().Be(0.0, "because that is the default value");
                fragment.Float.Should().Be(0.0f, "because that is the default value");
                fragment.Boolean.Should().Be(true, "because that is the non-zero value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
                fragment.Dictionary.Should().NotBeNull("because this fragment is of this type");
                fragment.Value
                    .Should()
                    .BeSameAs(fragment.Dictionary, "because both of these should access the same object");
                fragment.Dictionary
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
                fragment.String.Should().BeNull("because this fragment is not of this type");
                fragment.Dictionary.Should().BeNull("because this fragment is not of this type");
                fragment.Int.Should().Be(0, "because that is the default value");
                fragment.Long.Should().Be(0L, "because that is the default value");
                fragment.Double.Should().Be(0.0, "because that is the default value");
                fragment.Float.Should().Be(0.0f, "because that is the default value");
                fragment.Boolean.Should().Be(true, "because that is the non-zero value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
                fragment.Array.Should().NotBeNull("because this fragment is of this type");
                fragment.Value
                    .Should()
                    .BeSameAs(fragment.Array, "because both of these should access the same object");

                fragment.Array.ToList().ShouldAllBeEquivalentTo(references);
            });
        }

        [Fact]
        public void TestGetFragmentFromInteger()
        {
            var doc = new MutableDocument("doc1");
            doc.SetInt("integer", 10);
            SaveDocument(doc, d =>
            {
                var fragment = d["integer"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.String.Should().BeNull("because this fragment is not of this type");
                fragment.Array.Should().BeNull("because this fragment is not of this type");
                fragment.Dictionary.Should().BeNull("because this fragment is not of this type");
                fragment.Int.Should().Be(10, "because that is the stored value");
                fragment.Long.Should().Be(10L, "because that is the converted value");
                fragment.Double.Should().Be(10.0, "because that is the converted value");
                fragment.Float.Should().Be(10.0f, "because that is the converted value");
                fragment.Boolean.Should().Be(true, "because that is the converted value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromFloat()
        {
            var doc = new MutableDocument("doc1");
            doc.SetFloat("float", 100.10f);
            SaveDocument(doc, d =>
            {
                var fragment = d["float"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.String.Should().BeNull("because this fragment is not of this type");
                fragment.Array.Should().BeNull("because this fragment is not of this type");
                fragment.Dictionary.Should().BeNull("because this fragment is not of this type");
                fragment.Int.Should().Be(100, "because that is the stored value");
                fragment.Long.Should().Be(100L, "because that is the converted value");
                fragment.Float.Should().Be(100.10f, "because that is the stored value");
                fragment.Double.Should().BeApproximately(100.10, 0.0001, "because that is the converted value");
                fragment.Boolean.Should().Be(true, "because that is the converted value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromLong()
        {
            var doc = new MutableDocument("doc1");
            doc.SetLong("long", 10L);
            SaveDocument(doc, d =>
            {
                var fragment = d["long"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.String.Should().BeNull("because this fragment is not of this type");
                fragment.Array.Should().BeNull("because this fragment is not of this type");
                fragment.Dictionary.Should().BeNull("because this fragment is not of this type");
                fragment.Int.Should().Be(10, "because that is the converted value");
                fragment.Long.Should().Be(10L, "because that is the stored value");
                fragment.Double.Should().Be(10.0, "because that is the converted value");
                fragment.Float.Should().Be(10.0f, "because that is the converted value");
                fragment.Boolean.Should().Be(true, "because that is the converted value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromDouble()
        {
            var doc = new MutableDocument("doc1");
            doc.SetDouble("double", 100.10);
            SaveDocument(doc, d =>
            {
                var fragment = d["double"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.String.Should().BeNull("because this fragment is not of this type");
                fragment.Array.Should().BeNull("because this fragment is not of this type");
                fragment.Dictionary.Should().BeNull("because this fragment is not of this type");
                fragment.Int.Should().Be(100, "because that is the converted value");
                fragment.Long.Should().Be(100L, "because that is the converted value");
                fragment.Double.Should().Be(100.10, "because that is the stored value");
                fragment.Float.Should().Be(100.10f, "because that is the default value");
                fragment.Boolean.Should().Be(true, "because that is the converted value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromBoolean()
        {
            var doc = new MutableDocument("doc1");
            doc.SetBoolean("boolean", true);
            SaveDocument(doc, d =>
            {
                var fragment = d["boolean"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.String.Should().BeNull("because this fragment is not of this type");
                fragment.Array.Should().BeNull("because this fragment is not of this type");
                fragment.Dictionary.Should().BeNull("because this fragment is not of this type");
                fragment.Int.Should().Be(1, "because that is the converted value");
                fragment.Long.Should().Be(1L, "because that is the converted value");
                fragment.Double.Should().Be(1.0, "because that is the converted value");
                fragment.Float.Should().Be(1.0f, "because that is the converted value");
                fragment.Boolean.Should().Be(true, "because that is the stored value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromDate()
        {
            var date = DateTimeOffset.Now;
            var doc = new MutableDocument("doc1");
            doc.SetDate("date", date);
            SaveDocument(doc, d =>
            {
                var fragment = d["date"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.String.Should().NotBeNull("because this fragment is convertible to string");
                fragment.Array.Should().BeNull("because this fragment is not of this type");
                fragment.Dictionary.Should().BeNull("because this fragment is not of this type");
                fragment.Int.Should().Be(0, "because that is the default value");
                fragment.Long.Should().Be(0L, "because that is the default value");
                fragment.Double.Should().Be(0.0, "because that is the default value");
                fragment.Float.Should().Be(0.0f, "because that is the default value");
                fragment.Boolean.Should().Be(true, "because that is the non-zero value");
                fragment.Date.Should().Be(date, "because that is the stored value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
            });
        }

        [Fact]
        public void TestGetFragmentFromString()
        {
            var doc = new MutableDocument("doc1");
            doc.SetString("string", "hello world");
            SaveDocument(doc, d =>
            {
                var fragment = d["string"];
                fragment.Exists.Should().BeTrue("because this portion of the data exists");
                fragment.String.Should().Be("hello world", "because that is the stored value");
                fragment.Array.Should().BeNull("because this fragment is not of this type");
                fragment.Dictionary.Should().BeNull("because this fragment is not of this type");
                fragment.Int.Should().Be(0, "because that is the default value");
                fragment.Long.Should().Be(0L, "because that is the default value");
                fragment.Double.Should().Be(0.0, "because that is the default value");
                fragment.Float.Should().Be(0.0f, "because that is the default value");
                fragment.Boolean.Should().Be(true, "because that is the non-zero value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
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
                fragment.String.Should().BeNull("because this fragment is not of this type");
                fragment.Array.Should().BeNull("because this fragment is not of this type");
                fragment.Int.Should().Be(0, "because that is the default value");
                fragment.Long.Should().Be(0L, "because that is the default value");
                fragment.Double.Should().Be(0.0, "because that is the default value");
                fragment.Float.Should().Be(0.0f, "because that is the default value");
                fragment.Boolean.Should().Be(true, "because that is the non-zero value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
                fragment.Dictionary.Should().NotBeNull("because this fragment is of this type");
                fragment.Value
                    .Should()
                    .BeSameAs(fragment.Dictionary,
                        "because both of these accessors should return the same object");
                fragment.Dictionary.ShouldBeEquivalentTo(phones, "because that is the stored content");
                fragment.Dictionary.Count.Should().Be(1, "because there is one entry in the dictionary");
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
                fragment.String.Should().BeNull("because that is the default value");
                fragment.Array.Should().BeNull("because that is the default value");
                fragment.Int.Should().Be(0, "because that is the default value");
                fragment.Long.Should().Be(0L, "because that is the default value");
                fragment.Double.Should().Be(0.0, "because that is the default value");
                fragment.Float.Should().Be(0.0f, "because that is the default value");
                fragment.Boolean.Should().Be(false, "because that is the default value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().BeNull("because this fragment doesn't have a value");
                fragment.Dictionary.Should().BeNull("because that is the default value");
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
                fragment.String.Should().BeNull("because that is the default value");
                fragment.Int.Should().Be(0, "because that is the default value");
                fragment.Long.Should().Be(0L, "because that is the default value");
                fragment.Double.Should().Be(0.0, "because that is the default value");
                fragment.Float.Should().Be(0.0f, "because that is the default value");
                fragment.Boolean.Should().Be(true, "because that is the default value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().NotBeNull("because this fragment has a value");
                fragment.Dictionary.Should().BeNull("because that is the default value");
                fragment.Array.Should().NotBeNull("because this fragment is of this type");
                fragment.Value.As<ArrayObject>()
                    .Should()
                    .ContainInOrder(fragment.Array,
                        "because both of these accessors should return the same value");
                fragment.Array.Should().ContainInOrder(nested, "because that is what was stored");
                fragment.Array.Count.Should().Be(3, "because there are three elements inside");
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
                fragment.String.Should().BeNull("because that is the default value");
                fragment.Array.Should().BeNull("because that is the default value");
                fragment.Int.Should().Be(0, "because that is the default value");
                fragment.Long.Should().Be(0L, "because that is the default value");
                fragment.Double.Should().Be(0.0, "because that is the default value");
                fragment.Float.Should().Be(0.0f, "because that is the default value");
                fragment.Boolean.Should().Be(false, "because that is the default value");
                fragment.Date.Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                fragment.Value.Should().BeNull("because this fragment doesn't have a value");
                fragment.Dictionary.Should().BeNull("because that is the default value");
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
                d["string"].String.Should().Be("value", "because that is what was stored");
                d["bool"].Boolean.Should().BeTrue("because that is what was stored");
                d["int"].Int.Should().Be(7, "because that is what was stored");
                d["long"].Long.Should().Be(8L, "because that is what was stored");
                d["double"].Double.Should().Be(3.3, "because that is what was stored");
                d["float"].Float.Should().Be(2.2f, "because that is what was stored");
                d["date"].Date.Should().Be(date, "because that is what was stored");
            });
        }

        [Fact]
        public void TestDictionaryFragmentSetDictionary()
        {
            var doc = new MutableDocument("doc1");
            var dict = new MutableDictionary();
            dict.SetString("name", "Jason")
                .SetValue("address", new Dictionary<string, object> {
                    ["street"] = "1 Main Street",
                    ["phones"] = new Dictionary<string, object> {
                        ["mobile"] = "650-123-4567"
                    }
                });
            doc["dict"].Value = dict;
            SaveDocument(doc, d =>
            {
                d["dict"]["name"].String.Should().Be("Jason", "because that is what was stored");
                d["dict"]["address"]["street"]
                    .String
                    .Should()
                    .Be("1 Main Street", "because that is what was stored");
                d["dict"]["address"]["phones"]["mobile"]
                    .String
                    .Should()
                    .Be("650-123-4567", "because that is what was stored");
            });
        }

        [Fact]
        public void TestDicionaryFragmentSetArray()
        {
            var doc = new MutableDocument("doc1");
            var array = new MutableArray();
            array.AddInt(0).AddInt(1).AddInt(2);

            doc["array"].Value = array;
            SaveDocument(doc, d =>
            {
                d["array"][-1].Value.Should().BeNull("because that is an invalid index");
                d["array"][-1].Exists.Should().BeFalse("because there is no data at the invalid index");
                d["array"][0].Int.Should().Be(0, "because that is what was stored");
                d["array"][1].Int.Should().Be(1, "because that is what was stored");
                d["array"][2].Int.Should().Be(2, "because that is what was stored");
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
                d["dict"]["name"].String.Should().Be("Jason", "because that is what was stored");
                d["dict"]["address"]["street"]
                    .String
                    .Should()
                    .Be("1 Main Street", "because that is what was stored");
                d["dict"]["address"]["phones"]["mobile"]
                    .String
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
                d["dict"]["array"][0].Int.Should().Be(0, "because that is what was stored");
                d["dict"]["array"][1].Int.Should().Be(1, "because that is what was stored");
                d["dict"]["array"][2].Int.Should().Be(2, "because that is what was stored");
                d["dict"]["array"][3].Value.Should().BeNull("because that is an invalid index");
                d["dict"]["array"][3].Exists.Should().BeFalse("because there is no data at the invalid index");
            });

            doc.Dispose();
        }

        [Fact]
        public void TestNonDictionaryFragmentSetObject()
        {
            var doc = new MutableDocument("doc1");
            doc.SetString("string1", "value1").SetString("string2", "value2");
            SaveDocument(doc, d =>
            {
                var md = d.ToMutable();
                md["string1"].Value = 10;
                md["string1"].Int.Should().Be(10, "because the value was changed");
                md["string2"].String.Should().Be("value2", "because that is what was stored");
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

                d["array"][0].String.Should().Be("string", "because that is what was stored");
                d["array"][1].Int.Should().Be(10, "because that is what was stored");
                d["array"][2].Double.Should().Be(10.10, "because that is what was stored");
                d["array"][3].Boolean.Should().Be(true, "because that is what was stored");
                d["array"][4].Date.Should().Be(date, "because that is what was stored");
            });
        }

        [Fact]
        public void TestArrayFragmentSetDictionary()
        {
            var doc = new MutableDocument("doc1");
            var dict = new MutableDictionary();
            dict.SetString("name", "Jason")
                .SetValue("address", new Dictionary<string, object> {
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

                d["array"][0]["name"].String.Should().Be("Jason", "because that is what was stored");
                d["array"][0]["address"]["street"]
                    .String
                    .Should()
                    .Be("1 Main Street", "because that is what was stored");
                d["array"][0]["address"]["phones"]["mobile"]
                    .String
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
                .Array
                .AddValue(new Dictionary<string, object> {
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

                d["array"][0]["name"].String.Should().Be("Jason", "because that is what was stored");
                d["array"][0]["address"]["street"]
                    .String
                    .Should()
                    .Be("1 Main Street", "because that is what was stored");
                d["array"][0]["address"]["phones"]["mobile"]
                    .String
                    .Should()
                    .Be("650-123-4567", "because that is what was stored");
            });
        }

        [Fact]
        public void TestArrayFragmentSetArrayObject()
        {
            var doc = new MutableDocument("doc1");
            doc["array"].Value = new List<object>();
            var array = new MutableArray();
            array.AddString("Jason").AddDouble(5.5).AddBoolean(true);

            doc["array"].Array.AddArray(array);

            SaveDocument(doc, d =>
            {
                d["array"][0][0].String.Should().Be("Jason", "because that is the value that was stored");
                d["array"][0][1].Double.Should().Be(5.5, "because that is the value that was stored");
                d["array"][0][2].Boolean.Should().Be(true, "because that is the value that was stored");
            });
        }

        [Fact]
        public void TestArrayFragmentSetArray()
        {
            var doc = new MutableDocument("doc1");
            doc["array"].Value = new List<object>();
            doc["array"].Array.AddValue(new object[] {"Jason", 5.5, true});

            SaveDocument(doc, d =>
            {
                d["array"][0][0].String.Should().Be("Jason", "because that is the value that was stored");
                d["array"][0][1].Double.Should().Be(5.5, "because that is the value that was stored");
                d["array"][0][2].Boolean.Should().Be(true, "because that is the value that was stored");
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
                d["array"][0][0].Int.Should().Be(0);
                d["array"][0][1].Boolean.Should().Be(false);
                d["array"][0][2].String.Should().BeNull();
            });
        }

        [Fact]
        public void TestOutOfRangeArrayFragmentSetObject()
        {
            var doc = new MutableDocument("doc1");
            doc["array"].Value = new List<object>();
            doc["array"].Array.AddValue(new object[] { "Jason", 5.5, true });
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
            doc.SetData(new Dictionary<string, object> {
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

            doc["name"].String.Should().Be("Jason", "because that is what was stored");
            doc["address"]["street"].String.Should().Be("1 Main Street", "because that is what was stored");
            doc["address"]["phones"]["mobile"].String.Should().Be("650-123-4567", "because that is what was stored");
            doc["references"][0]["name"].String.Should().Be("Scott", "because that is what was stored");
            doc["references"][1]["name"].String.Should().Be("Sam", "because that is what was stored");

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

            doc["name"].String.Should().Be("Jason", "because that is what was stored");
            doc["address"]["street"].String.Should().Be("1 Main Street", "because that is what was stored");
            doc["address"]["phones"]["mobile"].String.Should().Be("650-123-4567", "because that is what was stored");
        }
    }
}

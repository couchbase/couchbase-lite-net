//
//  FragmentTest.cs
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
using Couchbase.Lite;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Test;

public sealed class FragmentTest(ITestOutputHelper output) : TestCase(output)
{
    [Fact]
    public void TestGetDocFragmentWithID()
    {
        var dict = new Dictionary<string, object?> {
            ["address"] = new Dictionary<string, object?> { 
                ["street"] = "1 Main street",
                ["city"] = "Mountain View",
                ["state"] = "CA"
            }
        };

        DefaultCollection.Save(new MutableDocument("doc1", dict));

        var doc = DefaultCollection["doc1"];
        doc.ShouldNotBeNull("because the subscript operator should never return null");
        doc.Exists.ShouldBeTrue("because the document was saved");
        doc.Document.ShouldNotBeNull("because the document exists");
        doc["address"]["street"].String.ShouldBe("1 Main street", "because that is what was stored");
        doc["address"]["city"].String.ShouldBe("Mountain View", "because that is what was stored");
        doc["address"]["state"].String.ShouldBe("CA", "because that is what was stored");
    }

    [Fact]
    public void TestGetDocFragmentWithNonExistingID()
    {
        var doc = DefaultCollection["doc1"];
        doc.ShouldNotBeNull("because the subscript operator should never return null");
        doc.Exists.ShouldBeFalse("because the document was never created");
        doc.Document.ShouldBeNull("because the document does not exist");
        doc["address"]["street"].String.ShouldBeNull("because the document does not exist");
        doc["address"]["city"].String.ShouldBeNull("because the document does not exist");
        doc["address"]["state"].String.ShouldBeNull("because the document does not exist");
    }

    [Fact]
    public void TestGetFragmentFromDictionaryValue()
    {
        var dict = new Dictionary<string, object?> {
            ["address"] = new Dictionary<string, object?> {
                ["street"] = "1 Main street",
                ["city"] = "Mountain View",
                ["state"] = "CA"
            }
        };

        var doc = new MutableDocument("doc1", dict);
        SaveDocument(doc, d =>
        {
            var fragment = d["address"];
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldBeNull("because this fragment is not of this type");
            fragment.Array.ShouldBeNull("because this fragment is not of this type");
            fragment.Int.ShouldBe(0, "because that is the default value");
            fragment.Long.ShouldBe(0L, "because that is the default value");
            fragment.Double.ShouldBe(0.0, "because that is the default value");
            fragment.Float.ShouldBe(0.0f, "because that is the default value");
            fragment.Boolean.ShouldBe(true, "because that is the non-zero value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
            fragment.Dictionary.ShouldNotBeNull("because this fragment is of this type");
            fragment.Value
                .ShouldBeSameAs(fragment.Dictionary, "because both of these should access the same object");
            fragment.Dictionary?
                .ToDictionary()
                .ShouldBeEquivalentToFluent(dict["address"], "because otherwise the contents are incorrect");
        });
    }

    [Fact]
    public void TestGetFragmentFromArrayValue()
    {
        var references =  new[] {
            new Dictionary<string, object ?> {
                ["name"] = "Scott"
            },
            new Dictionary<string, object?> {
                ["name"] = "Sam"
            }
        };

        var dict = new Dictionary<string, object?> {
            ["references"] = references
        };

        var doc = new MutableDocument("doc1", dict);
        SaveDocument(doc, d =>
        {
            var fragment = d["references"];
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldBeNull("because this fragment is not of this type");
            fragment.Dictionary.ShouldBeNull("because this fragment is not of this type");
            fragment.Int.ShouldBe(0, "because that is the default value");
            fragment.Long.ShouldBe(0L, "because that is the default value");
            fragment.Double.ShouldBe(0.0, "because that is the default value");
            fragment.Float.ShouldBe(0.0f, "because that is the default value");
            fragment.Boolean.ShouldBe(true, "because that is the non-zero value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
            fragment.Array.ShouldNotBeNull("because this fragment is of this type");
            fragment.Value
                .ShouldBeSameAs(fragment.Array, "because both of these should access the same object");

            references[0].ShouldBeEquivalentToFluent(fragment.Array?.GetDictionary(0));
            references[1].ShouldBeEquivalentToFluent(fragment.Array?.GetDictionary(1));
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
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldBeNull("because this fragment is not of this type");
            fragment.Array.ShouldBeNull("because this fragment is not of this type");
            fragment.Dictionary.ShouldBeNull("because this fragment is not of this type");
            fragment.Int.ShouldBe(10, "because that is the stored value");
            fragment.Long.ShouldBe(10L, "because that is the converted value");
            fragment.Double.ShouldBe(10.0, "because that is the converted value");
            fragment.Float.ShouldBe(10.0f, "because that is the converted value");
            fragment.Boolean.ShouldBe(true, "because that is the converted value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
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
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldBeNull("because this fragment is not of this type");
            fragment.Array.ShouldBeNull("because this fragment is not of this type");
            fragment.Dictionary.ShouldBeNull("because this fragment is not of this type");
            fragment.Int.ShouldBe(100, "because that is the stored value");
            fragment.Long.ShouldBe(100L, "because that is the converted value");
            fragment.Float.ShouldBe(100.10f, "because that is the stored value");
            fragment.Double.ShouldBe(100.10, 0.0001, "because that is the converted value");
            fragment.Boolean.ShouldBe(true, "because that is the converted value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
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
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldBeNull("because this fragment is not of this type");
            fragment.Array.ShouldBeNull("because this fragment is not of this type");
            fragment.Dictionary.ShouldBeNull("because this fragment is not of this type");
            fragment.Int.ShouldBe(10, "because that is the converted value");
            fragment.Long.ShouldBe(10L, "because that is the stored value");
            fragment.Double.ShouldBe(10.0, "because that is the converted value");
            fragment.Float.ShouldBe(10.0f, "because that is the converted value");
            fragment.Boolean.ShouldBe(true, "because that is the converted value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
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
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldBeNull("because this fragment is not of this type");
            fragment.Array.ShouldBeNull("because this fragment is not of this type");
            fragment.Dictionary.ShouldBeNull("because this fragment is not of this type");
            fragment.Int.ShouldBe(100, "because that is the converted value");
            fragment.Long.ShouldBe(100L, "because that is the converted value");
            fragment.Double.ShouldBe(100.10, "because that is the stored value");
            fragment.Float.ShouldBe(100.10f, "because that is the default value");
            fragment.Boolean.ShouldBe(true, "because that is the converted value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
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
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldBeNull("because this fragment is not of this type");
            fragment.Array.ShouldBeNull("because this fragment is not of this type");
            fragment.Dictionary.ShouldBeNull("because this fragment is not of this type");
            fragment.Int.ShouldBe(1, "because that is the converted value");
            fragment.Long.ShouldBe(1L, "because that is the converted value");
            fragment.Double.ShouldBe(1.0, "because that is the converted value");
            fragment.Float.ShouldBe(1.0f, "because that is the converted value");
            fragment.Boolean.ShouldBe(true, "because that is the stored value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
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
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldNotBeNull("because this fragment is convertible to string");
            fragment.Array.ShouldBeNull("because this fragment is not of this type");
            fragment.Dictionary.ShouldBeNull("because this fragment is not of this type");
            fragment.Int.ShouldBe(0, "because that is the default value");
            fragment.Long.ShouldBe(0L, "because that is the default value");
            fragment.Double.ShouldBe(0.0, "because that is the default value");
            fragment.Float.ShouldBe(0.0f, "because that is the default value");
            fragment.Boolean.ShouldBe(true, "because that is the non-zero value");
            fragment.Date.ShouldBe(date, "because that is the stored value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
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
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldBe("hello world", "because that is the stored value");
            fragment.Array.ShouldBeNull("because this fragment is not of this type");
            fragment.Dictionary.ShouldBeNull("because this fragment is not of this type");
            fragment.Int.ShouldBe(0, "because that is the default value");
            fragment.Long.ShouldBe(0L, "because that is the default value");
            fragment.Double.ShouldBe(0.0, "because that is the default value");
            fragment.Float.ShouldBe(0.0f, "because that is the default value");
            fragment.Boolean.ShouldBe(true, "because that is the non-zero value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
        });
    }

    [Fact]
    public void TestGetNestedDictionaryFragment()
    {
        var phones = new Dictionary<string, object?> {
            ["mobile"] = "650-123-4567"
        };

        var dict = new Dictionary<string, object?> {
            ["address"] = new Dictionary<string, object?> {
                ["street"] = "1 Main street",
                ["phones"] = phones
            }
        };

        var doc = new MutableDocument("doc1", dict);
        SaveDocument(doc, d =>
        {
            var fragment = d["address"]["phones"];
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldBeNull("because this fragment is not of this type");
            fragment.Array.ShouldBeNull("because this fragment is not of this type");
            fragment.Int.ShouldBe(0, "because that is the default value");
            fragment.Long.ShouldBe(0L, "because that is the default value");
            fragment.Double.ShouldBe(0.0, "because that is the default value");
            fragment.Float.ShouldBe(0.0f, "because that is the default value");
            fragment.Boolean.ShouldBe(true, "because that is the non-zero value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
            fragment.Dictionary.ShouldNotBeNull("because this fragment is of this type");
            fragment.Value
                .ShouldBeSameAs(fragment.Dictionary,
                    "because both of these accessors should return the same object");
            fragment.Dictionary.ShouldBeEquivalentToFluent(phones, "because that is the stored content");
            fragment.Dictionary.Count.ShouldBe(1, "because there is one entry in the dictionary");
        });
    }

    [Fact]
    public void TestGetNestedNonExistingDictionaryFragment()
    {
        var phones = new Dictionary<string, object?> {
            ["mobile"] = "650-123-4567"
        };

        var dict = new Dictionary<string, object?> {
            ["address"] = new Dictionary<string, object?> {
                ["street"] = "1 Main street",
                ["phones"] = phones
            }
        };

        var doc = new MutableDocument("doc1", dict);
        SaveDocument(doc, d =>
        {
            var fragment = d["address"]["country"];
            fragment.Exists.ShouldBeFalse("because this portion of the data doesn't exist");
            fragment.String.ShouldBeNull("because that is the default value");
            fragment.Array.ShouldBeNull("because that is the default value");
            fragment.Int.ShouldBe(0, "because that is the default value");
            fragment.Long.ShouldBe(0L, "because that is the default value");
            fragment.Double.ShouldBe(0.0, "because that is the default value");
            fragment.Float.ShouldBe(0.0f, "because that is the default value");
            fragment.Boolean.ShouldBe(false, "because that is the default value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldBeNull("because this fragment doesn't have a value");
            fragment.Dictionary.ShouldBeNull("because that is the default value");
        });
    }

    [Fact]
    public void TestGetNestedArrayFragments()
    {
        var nested = new[] {4L, 5L, 6L};
        var dict = new Dictionary<string, object?> {
            ["nested-array"] = new object?[] {
                new[] {1, 2, 3},
                nested
            }
        };

        var doc = new MutableDocument("doc1", dict);
        SaveDocument(doc, d =>
        {
            var fragment = d["nested-array"][1];
            fragment.Exists.ShouldBeTrue("because this portion of the data exists");
            fragment.String.ShouldBeNull("because that is the default value");
            fragment.Int.ShouldBe(0, "because that is the default value");
            fragment.Long.ShouldBe(0L, "because that is the default value");
            fragment.Double.ShouldBe(0.0, "because that is the default value");
            fragment.Float.ShouldBe(0.0f, "because that is the default value");
            fragment.Boolean.ShouldBe(true, "because that is the default value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldNotBeNull("because this fragment has a value");
            fragment.Dictionary.ShouldBeNull("because that is the default value");
            fragment.Array.ShouldNotBeNull("because this fragment is of this type");
            fragment.Value.ShouldBeEquivalentToFluent(fragment.Array,
                "because both of these accessors should return the same value");
            fragment.Array.Count.ShouldBe(3, "because there are three elements inside");
            var list = fragment.Array!.ToList();
            for (int i = 0; i < fragment.Array.Count; i++)
                list[i].ShouldBe(nested[i]);
        });
    }

    [Fact]
    public void TestGetNestedNonExistingArrayFragments()
    {
        var nested = new[] { 1, 2, 3 };
        var dict = new Dictionary<string, object?> {
            ["nested-array"] = new object?[] {
                nested,
                new[] {4, 5, 6}
            }
        };

        var doc = new MutableDocument("doc1", dict);
        SaveDocument(doc, d =>
        {
            var fragment = d["nested-array"][2];
            fragment.Exists.ShouldBeFalse("because this portion of the data doesn't exist");
            fragment.String.ShouldBeNull("because that is the default value");
            fragment.Array.ShouldBeNull("because that is the default value");
            fragment.Int.ShouldBe(0, "because that is the default value");
            fragment.Long.ShouldBe(0L, "because that is the default value");
            fragment.Double.ShouldBe(0.0, "because that is the default value");
            fragment.Float.ShouldBe(0.0f, "because that is the default value");
            fragment.Boolean.ShouldBe(false, "because that is the default value");
            fragment.Date.ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
            fragment.Value.ShouldBeNull("because this fragment doesn't have a value");
            fragment.Dictionary.ShouldBeNull("because that is the default value");
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
            d["string"].String.ShouldBe("value", "because that is what was stored");
            d["bool"].Boolean.ShouldBeTrue("because that is what was stored");
            d["int"].Int.ShouldBe(7, "because that is what was stored");
            d["long"].Long.ShouldBe(8L, "because that is what was stored");
            d["double"].Double.ShouldBe(3.3, "because that is what was stored");
            d["float"].Float.ShouldBe(2.2f, "because that is what was stored");
            d["date"].Date.ShouldBe(date, "because that is what was stored");
        });
    }

    [Fact]
    public void TestDictionaryFragmentSetDictionary()
    {
        var doc = new MutableDocument("doc1");
        var dict = new MutableDictionaryObject();
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
            d["dict"]["name"].String.ShouldBe("Jason", "because that is what was stored");
            d["dict"]["address"]["street"]
                .String
                .ShouldBe("1 Main Street", "because that is what was stored");
            d["dict"]["address"]["phones"]["mobile"]
                .String
                .ShouldBe("650-123-4567", "because that is what was stored");
        });
    }

    [Fact]
    public void TestDictionaryFragmentSetArray()
    {
        var doc = new MutableDocument("doc1");
        var array = new MutableArrayObject();
        array.AddInt(0).AddInt(1).AddInt(2);

        doc["array"].Value = array;
        SaveDocument(doc, d =>
        {
            d["array"][-1].Value.ShouldBeNull("because that is an invalid index");
            d["array"][-1].Exists.ShouldBeFalse("because there is no data at the invalid index");
            d["array"][0].Int.ShouldBe(0, "because that is what was stored");
            d["array"][1].Int.ShouldBe(1, "because that is what was stored");
            d["array"][2].Int.ShouldBe(2, "because that is what was stored");
            d["array"][3].Value.ShouldBeNull("because that is an invalid index");
            d["array"][3].Exists.ShouldBeFalse("because there is no data at the invalid index");
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
            d["dict"]["name"].String.ShouldBe("Jason", "because that is what was stored");
            d["dict"]["address"]["street"]
                .String
                .ShouldBe("1 Main Street", "because that is what was stored");
            d["dict"]["address"]["phones"]["mobile"]
                .String
                .ShouldBe("650-123-4567", "because that is what was stored");
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
            d["dict"]["array"][-1].Value.ShouldBeNull("because that is an invalid index");
            d["dict"]["array"][-1].Exists.ShouldBeFalse("because there is no data at the invalid index");
            d["dict"]["array"][0].Int.ShouldBe(0, "because that is what was stored");
            d["dict"]["array"][1].Int.ShouldBe(1, "because that is what was stored");
            d["dict"]["array"][2].Int.ShouldBe(2, "because that is what was stored");
            d["dict"]["array"][3].Value.ShouldBeNull("because that is an invalid index");
            d["dict"]["array"][3].Exists.ShouldBeFalse("because there is no data at the invalid index");
        });

        doc.Dispose();
    }

    [Fact]
    public void TestNonDictionaryFragmentSetValue()
    {
        var doc = new MutableDocument("doc1");
        doc.SetString("string1", "value1").SetString("string2", "value2");
        SaveDocument(doc, d =>
        {
            var md = d.ToMutable();
            md["string1"].Value = 10;
            md["string1"].Int.ShouldBe(10, "because the value was changed");
            md["string2"].String.ShouldBe("value2", "because that is what was stored");
            md.Dispose();
        });

        doc.Dispose();
    }

    [Fact]
    public void TestArrayFragmentSet()
    {
        var date = DateTimeOffset.Now;
        var doc = new MutableDocument("doc1");
        doc["array"].Value = new object?[] {
            "string",
            10,
            10.10,
            true,
            date
        };

        SaveDocument(doc, d =>
        {
            d["array"][-1].ShouldNotBeNull("because the subscript operator should never return null");
            d["array"][-1].Exists.ShouldBeFalse("because there is no data at an invalid index");
            for (int i = 0; i < 5; i++) {
                d["array"][i].ShouldNotBeNull("because the subscript operator should never return null");
                d["array"][i].Exists.ShouldBeTrue("because there is data at this index");
            }

            d["array"][5].ShouldNotBeNull("because the subscript operator should never return null");
            d["array"][5].Exists.ShouldBeFalse("because there is no data at an invalid index");

            d["array"][0].String.ShouldBe("string", "because that is what was stored");
            d["array"][1].Int.ShouldBe(10, "because that is what was stored");
            d["array"][2].Double.ShouldBe(10.10, "because that is what was stored");
            d["array"][3].Boolean.ShouldBe(true, "because that is what was stored");
            d["array"][4].Date.ShouldBe(date, "because that is what was stored");
        });
    }

    [Fact]
    public void TestArrayFragmentSetDictionary()
    {
        var doc = new MutableDocument("doc1");
        var dict = new MutableDictionaryObject();
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
            d["array"][-1].Value.ShouldBeNull("because that is an invalid index");
            d["array"][-1].Exists.ShouldBeFalse("because there is no data at the invalid index");
            d["array"][0].Exists.ShouldBeTrue("because data exists at this index");
            d["array"][1].Value.ShouldBeNull("because that is an invalid index");
            d["array"][1].Exists.ShouldBeFalse("because there is no data at the invalid index");

            d["array"][0]["name"].String.ShouldBe("Jason", "because that is what was stored");
            d["array"][0]["address"]["street"]
                .String
                .ShouldBe("1 Main Street", "because that is what was stored");
            d["array"][0]["address"]["phones"]["mobile"]
                .String
                .ShouldBe("650-123-4567", "because that is what was stored");
        });
    }

    [Fact]
    public void TestArrayFragmentSetCSharpDictionary()
    {
        var doc = new MutableDocument("doc1");
        doc["array"].Value = new List<object?>();
        doc["array"]
            .Array!
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
            d["array"][-1].Value.ShouldBeNull("because that is an invalid index");
            d["array"][-1].Exists.ShouldBeFalse("because there is no data at the invalid index");
            d["array"][0].Exists.ShouldBeTrue("because data exists at this index");
            d["array"][1].Value.ShouldBeNull("because that is an invalid index");
            d["array"][1].Exists.ShouldBeFalse("because there is no data at the invalid index");

            d["array"][0]["name"].String.ShouldBe("Jason", "because that is what was stored");
            d["array"][0]["address"]["street"]
                .String
                .ShouldBe("1 Main Street", "because that is what was stored");
            d["array"][0]["address"]["phones"]["mobile"]
                .String
                .ShouldBe("650-123-4567", "because that is what was stored");
        });
    }

    [Fact]
    public void TestArrayFragmentSetArrayObject()
    {
        var doc = new MutableDocument("doc1");
        doc["array"].Value = new List<object?>();
        var array = new MutableArrayObject();
        array.AddString("Jason").AddDouble(5.5).AddBoolean(true);

        doc["array"].Array!.AddArray(array);

        SaveDocument(doc, d =>
        {
            d["array"][0][0].String.ShouldBe("Jason", "because that is the value that was stored");
            d["array"][0][1].Double.ShouldBe(5.5, "because that is the value that was stored");
            d["array"][0][2].Boolean.ShouldBe(true, "because that is the value that was stored");
        });
    }

    [Fact]
    public void TestArrayFragmentSetArray()
    {
        var doc = new MutableDocument("doc1");
        doc["array"].Value = new List<object?>();
        doc["array"].Array!.AddValue(new object?[] {"Jason", 5.5, true});

        SaveDocument(doc, d =>
        {
            d["array"][0][0].String.ShouldBe("Jason", "because that is the value that was stored");
            d["array"][0][1].Double.ShouldBe(5.5, "because that is the value that was stored");
            d["array"][0][2].Boolean.ShouldBe(true, "because that is the value that was stored");
        });
    }

    [Fact]
    public void TestNonExistingArrayFragmentSetObject()
    {
        var doc = new MutableDocument("doc1");

        Should.Throw<InvalidOperationException>(() => doc["array"][0][0].Value = 1,
            "because the path does not exist");
        Should.Throw<InvalidOperationException>(() => doc["array"][0][1].Value = false,
            "because the path does not exist");
        Should.Throw<InvalidOperationException>(() => doc["array"][0][2].Value = "hello",
            "because the path does not exist");

        SaveDocument(doc, d =>
        {
            d["array"][0][0].Int.ShouldBe(0);
            d["array"][0][1].Boolean.ShouldBe(false);
            d["array"][0][2].String.ShouldBeNull();
        });
    }

    [Fact]
    public void TestOutOfRangeArrayFragmentSetObject()
    {
        var doc = new MutableDocument("doc1");
        doc["array"].Value = new List<object?>();
        doc["array"].Array!.AddValue(new object?[] { "Jason", 5.5, true });
        Should.Throw<InvalidOperationException>(() => doc["array"][0][3].Value = 1);

        SaveDocument(doc, d =>
        {
            d["array"][0][3].Exists.ShouldBeFalse();
        });
    }

    [Fact]
    public void TestBasicGetFragmentValues()
    {
        var doc = new MutableDocument("doc1");
        doc.SetData(new Dictionary<string, object?> {
            ["name"] = "Jason",
            ["address"] = new Dictionary<string, object?> {
                ["street"] = "1 Main Street",
                ["phones"] = new Dictionary<string, object?> {
                    ["mobile"] = "650-123-4567"
                }
            },
            ["references"] = new[] {
                new Dictionary<string, object?> {
                    ["name"] = "Scott"
                },
                new Dictionary<string, object?> {
                    ["name"] = "Sam"
                }
            }
        });

        doc["name"].String.ShouldBe("Jason", "because that is what was stored");
        doc["address"]["street"].String.ShouldBe("1 Main Street", "because that is what was stored");
        doc["address"]["phones"]["mobile"].String.ShouldBe("650-123-4567", "because that is what was stored");
        doc["references"][0]["name"].String.ShouldBe("Scott", "because that is what was stored");
        doc["references"][1]["name"].String.ShouldBe("Sam", "because that is what was stored");

        doc["references"][2]["name"].Value.ShouldBeNull("because this is an invalid index");
        doc["dummy"]["dummy"]["dummy"].Value.ShouldBeNull("because these are invalid keys");
        doc["dummy"]["dummy"][0]["dummy"].Value.ShouldBeNull("because these are invalid keys and indices");
    }

    [Fact]
    public void TestBasicSetFragmentValues()
    {
        var doc = new MutableDocument("doc1");
        doc["name"].Value = "Jason";

        doc["address"].Value = new MutableDictionaryObject();
        doc["address"]["street"].Value = "1 Main Street";
        doc["address"]["phones"].Value = new MutableDictionaryObject();
        doc["address"]["phones"]["mobile"].Value = "650-123-4567";

        doc["name"].String.ShouldBe("Jason", "because that is what was stored");
        doc["address"]["street"].String.ShouldBe("1 Main Street", "because that is what was stored");
        doc["address"]["phones"]["mobile"].String.ShouldBe("650-123-4567", "because that is what was stored");
    }
}
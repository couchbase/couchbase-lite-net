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
using System.Text.Json;
using Couchbase.Lite;
using Couchbase.Lite.Internal.Doc;

using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Test;

public sealed class DictionaryTest(ITestOutputHelper output) : TestCase(output)
{
    [Fact]
    public void TestCreateDictionary()
    {
        var address = new MutableDictionaryObject();
        address.Count.ShouldBe(0, "because the dictionary is empty");
        address.ToDictionary().ShouldBeEmpty("because the dictionary is empty");

        var doc1 = new MutableDocument("doc1");
        doc1.SetDictionary("address", address);
        doc1.GetDictionary("address")
            .ShouldBeSameAs(address, "because the document should return the same instance");

        DefaultCollection.Save(doc1);
        var gotDoc = DefaultCollection.GetDocument("doc1");
        gotDoc?.GetDictionary("address").ShouldNotBeNull("because the document was just saved");
        gotDoc!.GetDictionary("address")!.ToDictionary().ShouldBeEmpty("because the content should not have changed");
    }

    [Fact]
    public void TestCreateDictionaryWithCSharpDictionary()
    {
        var dict = new Dictionary<string, object?> {
            ["street"] = "1 Main street",
            ["city"] = "Mountain View",
            ["state"] = "CA"
        };
        var address = new MutableDictionaryObject(dict);
        address.ShouldBeEquivalentToFluent(dict, "because that is what was stored");
        address.ToDictionary().ShouldBeEquivalentToFluent(dict, "because that is what was stored");

        var doc1 = new MutableDocument("doc1");
        doc1.SetDictionary("address", address);
        doc1.GetDictionary("address")
            .ShouldBeSameAs(address, "because the document should return the same instance");

        DefaultCollection.Save(doc1);
        var gotDoc = DefaultCollection.GetDocument("doc1");
        gotDoc?.GetDictionary("address").ShouldNotBeNull();
        gotDoc!.GetDictionary("address")!
            .ToDictionary()
            .ShouldBeEquivalentToFluent(dict, "because the content should not have changed");
    }

    [Fact]
    public void TestGetValueFromNewEmptyDictionary()
    {
        DictionaryObject dict = new MutableDictionaryObject();
        dict.GetInt("key").ShouldBe(0, "because that is the default value");
        dict.GetLong("key").ShouldBe(0L, "because that is the default value");
        dict.GetDouble("key").ShouldBe(0.0, "because that is the default value");
        dict.GetBoolean("key").ShouldBe(false, "because that is the default value");
        dict.GetDate("key").ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
        dict.GetBlob("key").ShouldBeNull("because that is the default value");
        dict.GetValue("key").ShouldBeNull("because that is the default value");
        dict.GetString("key").ShouldBeNull("because that is the default value");
        dict.GetDictionary("key").ShouldBeNull("because that is the default value");
        dict.GetArray("key").ShouldBeNull("because that is the default value");
        dict.ToDictionary().ShouldBeEmpty("because the dictionary is empty");

        var doc = new MutableDocument("doc1");
        doc.SetDictionary("dict", dict);

        DefaultCollection.Save(doc);
        var gotDoc = DefaultCollection.GetDocument("doc1");
        gotDoc?.GetDictionary("dict").ShouldNotBeNull("because the document was just saved");
        dict = gotDoc!.GetDictionary("dict")!;
        dict.GetInt("key").ShouldBe(0, "because that is the default value");
        dict.GetLong("key").ShouldBe(0L, "because that is the default value");
        dict.GetDouble("key").ShouldBe(0.0, "because that is the default value");
        dict.GetBoolean("key").ShouldBe(false, "because that is the default value");
        dict.GetDate("key").ShouldBe(DateTimeOffset.MinValue, "because that is the default value");
        dict.GetBlob("key").ShouldBeNull("because that is the default value");
        dict.GetValue("key").ShouldBeNull("because that is the default value");
        dict.GetString("key").ShouldBeNull("because that is the default value");
        dict.GetDictionary("key").ShouldBeNull("because that is the default value");
        dict.GetArray("key").ShouldBeNull("because that is the default value");
        dict.ToDictionary().ShouldBeEmpty("because the dictionary is empty");
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

        doc.GetDictionary("level1").ShouldBeEquivalentToFluent(level1, "because that is what was inserted");
        level1.GetDictionary("level2").ShouldBeEquivalentToFluent(level2, "because that is what was inserted");
        level2.GetDictionary("level3").ShouldBeEquivalentToFluent(level3, "because that is what was inserted");
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

        doc.ToDictionary().ShouldBeEquivalentToFluent(dict, "because otherwise the document's contents are incorrect");

        DefaultCollection.Save(doc);
        var gotDoc = DefaultCollection.GetDocument("doc1");
        gotDoc.ShouldNotBeNull("because the document was just saved");
        gotDoc.GetDictionary("level1").ShouldNotBeSameAs(level1);
        gotDoc.ToDictionary().ShouldBeEquivalentToFluent(dict);
    }

    [Fact]
    public void TestSetNull()
    {
        using var mDoc = new MutableDocument("test");
        var mDict = new MutableDictionaryObject();
        mDict.SetValue("obj-null", null);
        mDict.SetString("string-null", null);
        mDict.SetArray("array-null", null);
        mDict.SetDictionary("dict-null", null);
        mDoc.SetDictionary("dict", mDict);
        SaveDocument(mDoc, doc =>
        {
            doc.Count.ShouldBe(1);
            doc.Contains("dict").ShouldBeTrue();
            var d = doc.GetDictionary("dict");
            d.ShouldNotBeNull();
            d.Count.ShouldBe(4);
            d.Contains("obj-null").ShouldBeTrue(); // If null the previous check will fail
            d.Contains("string-null").ShouldBeTrue();
            d.Contains("array-null").ShouldBeTrue();
            d.Contains("dict-null").ShouldBeTrue();
            d.GetValue("obj-null").ShouldBeNull();
            d.GetValue("string-null").ShouldBeNull();
            d.GetValue("array-null").ShouldBeNull();
            d.GetValue("dict-null").ShouldBeNull();
        });
    }

    [Fact]
    public void TestSetOthers()
    {
        // Uncovered by other tests
        var dict = new MutableDictionaryObject();
        dict.SetFloat("pi", 3.14f);
        dict.SetDouble("better_pi", 3.14159);
        dict.SetBoolean("use_better", true);

        dict.GetFloat("pi").ShouldBe(3.14f);
        dict.GetDouble("better_pi").ShouldBe(3.14159);
        dict.GetDouble("pi").ShouldBe(3.14, 0.00001);
        dict.GetFloat("better_pi").ShouldBe(3.14159f, 0.0000000001f);
        dict.GetBoolean("use_better").ShouldBeTrue();
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

        doc.SetData(new Dictionary<string, object?> {
            ["dictionaries"] = data
        });

        var dictionaries = doc.GetArray("dictionaries");
        dictionaries.ShouldNotBeNull();
        dictionaries.Count.ShouldBe(4, "because that is the number of entries added");

        var d1 = dictionaries.GetDictionary(0); // This is not null because otherwise the previous check will fail
        var d2 = dictionaries.GetDictionary(1);
        var d3 = dictionaries.GetDictionary(2);
        var d4 = dictionaries.GetDictionary(3);

        d1?.GetString("name").ShouldBe("1", "because that is what was stored");
        d2?.GetString("name").ShouldBe("2", "because that is what was stored");
        d3?.GetString("name").ShouldBe("3", "because that is what was stored");
        d4?.GetString("name").ShouldBe("4", "because that is what was stored");

        DefaultCollection.Save(doc);
        var gotDoc = DefaultCollection.GetDocument("doc1");
        gotDoc.ShouldNotBeNull("because the document was just saved");
        var savedDictionaries = gotDoc.GetArray("dictionaries");
        savedDictionaries.ShouldNotBeNull();
        savedDictionaries.Count.ShouldBe(4, "because that is the number of entries");

        var savedD1 = savedDictionaries.GetDictionary(0);
        var savedD2 = savedDictionaries.GetDictionary(1);
        var savedD3 = savedDictionaries.GetDictionary(2);
        var savedD4 = savedDictionaries.GetDictionary(3);
        savedD1?.GetString("name").ShouldBe("1", "because that is what was stored");
        savedD2?.GetString("name").ShouldBe("2", "because that is what was stored");
        savedD3?.GetString("name").ShouldBe("3", "because that is what was stored");
        savedD4?.GetString("name").ShouldBe("4", "because that is what was stored");
    }

    [Fact]
    public void TestReplaceDictionary()
    {
        var doc = new MutableDocument("doc1");
        var profile1 = new MutableDictionaryObject();
        profile1.SetString("name", "Scott Tiger");
        doc.SetDictionary("profile", profile1);
        doc.GetDictionary("profile").ShouldBeEquivalentToFluent(profile1, "because that is what was set");

        var profile2 = new MutableDictionaryObject();
        profile2.SetString("name", "Daniel Tiger");
        doc.SetDictionary("profile", profile2);
        doc.GetDictionary("profile").ShouldBeEquivalentToFluent(profile2, "because that is what was set");

        profile1.SetInt("age", 20);
        profile1.GetString("name").ShouldBe("Scott Tiger", "because profile1 should be detached now");
        profile1.GetInt("age").ShouldBe(20, "because profile1 should be detached now");

        profile2.GetString("name").ShouldBe("Daniel Tiger", "because profile2 should be unchanged");
        profile2.GetValue("age").ShouldBeNull("because profile2 should be unchanged");

        DefaultCollection.Save(doc);
        var gotDoc = DefaultCollection.GetDocument("doc1");
        gotDoc?.GetDictionary("profile").ShouldNotBeNull("because it was saved into doc1");

        gotDoc!.GetDictionary("profile")
            .ShouldNotBeSameAs(profile2, "because a new MutableDocument should return a new instance");
        var savedProfile2 = gotDoc.GetDictionary("profile");
        savedProfile2?.GetString("name").ShouldBe("Daniel Tiger", "because that is what was saved");
    }

    [Fact]
    public void TestReplaceDictionaryDifferentType()
    {
        var doc = new MutableDocument("doc1");
        var profile1 = new MutableDictionaryObject();
        profile1.SetString("name", "Scott Tiger");
        doc.SetDictionary("profile", profile1);
        doc.GetDictionary("profile").ShouldBeEquivalentToFluent(profile1, "because that is what was set");

        doc.SetString("profile", "Daniel Tiger");
        doc.GetString("profile").ShouldBe("Daniel Tiger", "because that is what was set");

        profile1.SetInt("age", 20);
        profile1.GetString("name").ShouldBe("Scott Tiger", "because profile1 should be detached now");
        profile1.GetInt("age").ShouldBe(20, "because profile1 should be detached now");

        doc.GetString("profile").ShouldBe("Daniel Tiger", "because profile1 should not affect the new value");

        DefaultCollection.Save(doc);
        var gotDoc = DefaultCollection.GetDocument("doc1");
        gotDoc?.GetString("profile").ShouldBe("Daniel Tiger", "because that is what was saved");
    }

    [Fact]
    public void TestRemoveDictionary()
    {
        var doc = new MutableDocument("doc1");
        var profile1 = new MutableDictionaryObject();
        profile1.SetString("name", "Scott Tiger");
        doc.SetDictionary("profile", profile1);
        doc.GetDictionary("profile").ShouldBeEquivalentToFluent(profile1, "because that was what was inserted");
        doc.Contains("profile").ShouldBeTrue("because a value exists for that key");

        doc.Remove("profile");
        doc.GetValue("profile").ShouldBeNull("because the value for 'profile' was removed");
        doc.Contains("profile").ShouldBeFalse("because the value was removed");

        profile1.SetInt("age", 20);
        profile1.GetString("name").ShouldBe("Scott Tiger", "because the dictionary object should be unaffected");
        profile1.GetInt("age").ShouldBe(20, "because the dictionary should still be editable");

        doc.GetValue("profile")
            .ShouldBeNull("because changes to the dictionary should not have any affect anymore");

        SaveDocument(doc, d =>
        {
            d.GetValue("profile").ShouldBeNull("because the value for 'profile' was removed");
            d.Contains("profile").ShouldBeFalse("because the value was removed");
        });
    }

    [Fact]
    public void TestEnumeratingDictionary()
    {
        var dict = new MutableDictionaryObject();
        for (var i = 0; i < 20; i++) {
            dict.SetInt($"key{i}", i);
        }

        var content = dict.ToDictionary();
        var result = new Dictionary<string, object?>();
        foreach (var item in dict) {
            result[item.Key] = item.Value;
        }

        result.ShouldBeEquivalentToFluent(content, "because that is the correct content");
        content = dict.Remove("key2").SetInt("key20", 20).SetInt("key21", 21).ToDictionary();

        result = new();
        foreach (var item in dict) {
            result[item.Key] = item.Value;
        }

        result.ShouldBeEquivalentToFluent(content, "because that is the correct content");

        var doc = new MutableDocument("doc1");
        doc.SetDictionary("dict", dict);
        SaveDocument(doc, d =>
        {
            result = new();
            var dictObj = d.GetDictionary("dict");
            dictObj.ShouldNotBeNull("because it was just saved into the document");
            foreach (var item in dictObj)
            {
                result[item.Key] = item.Value;
            }

            result.ShouldBeEquivalentToFluent(content, "because that is the correct content");
        });
    }

    [ForIssue("couchbase-lite-core/230")]
    [Fact]
    public void TestLargeLongValue()
    {
        using var doc = new MutableDocument("test");
        const long num1 = 1234567L;
        const long num2 = 12345678L;
        const long num3 = 123456789L;
        doc.SetLong("num1", num1);
        doc.SetLong("num2", num2);
        doc.SetLong("num3", num3);
        DefaultCollection.Save(doc);
        using var newDoc = DefaultCollection.GetDocument(doc.Id)?.ToMutable();
        newDoc.ShouldNotBeNull("because the document was just saved");
        newDoc.GetLong("num1").ShouldBe(num1);
        newDoc.GetLong("num2").ShouldBe(num2);
        newDoc.GetLong("num3").ShouldBe(num3);
    }

    [Fact]
    public void TestLargeLongValue2()
    {
        // https://forums.couchbase.com/t/long-value-on-document-changed-after-saved-to-db/14259
        using var doc = new MutableDocument("test");
        const long num1 = 11989091L;
        const long num2 = 231548688L;
        doc.SetLong("num1", num1);
        doc.SetLong("num2", num2);
        DefaultCollection.Save(doc);
        using var newDoc = DefaultCollection.GetDocument(doc.Id)?.ToMutable();
        newDoc.ShouldNotBeNull("because the document was just saved");
        newDoc.GetLong("num1").ShouldBe(num1);
        newDoc.GetLong("num2").ShouldBe(num2);
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

        using var mDoc = new MutableDocument("test");
        mDoc.SetDictionary("dict", mDict);
                
        DefaultCollection.Save(mDoc);
        using var doc = DefaultCollection.GetDocument(mDoc.Id)?.ToMutable();
        var dict = doc?.GetDictionary("dict");
        dict.ShouldNotBeNull();
        dict.GetDictionary("not-exists").ShouldBeNull();
        var nestedDict = dict.GetDictionary("nestedDict");
        nestedDict.ShouldNotBeNull();
        nestedDict.ToDictionary().ShouldBeEquivalentToFluent(mNestedDict.ToDictionary());
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

        using var mDoc = new MutableDocument("test");
        mDoc.SetArray("array", mArray);
                
        DefaultCollection.Save(mDoc);
        using var doc = DefaultCollection.GetDocument(mDoc.Id)?.ToMutable();
        var array = doc?.GetArray("array");
        array.ShouldNotBeNull();
        array.GetArray(0).ShouldBeNull();
        array.GetArray(1).ShouldBeNull();
        array.GetArray(2).ShouldBeNull();
        array.GetArray(3).ShouldNotBeNull();

        var nestedArray = array.GetArray(3);
        nestedArray.ShouldBeEquivalentToFluent(mNestedArray);
        array.ShouldBeEquivalentToFluent(mArray);
    }

    [Fact]
    public void TestTypes()
    {
        var dict = new MutableDictionaryObject();
        var dict2 = new InMemoryDictionary();
        foreach (var d in new IMutableDictionary[] { dict, dict2 }) {
            Should.Throw<ArgumentException>(() => d.SetValue("test", new ASCIIEncoding()));
            Should.Throw<ArgumentException>(() => d.SetValue("test", new[] { new ASCIIEncoding() }));
            Should.Throw<ArgumentException>(() => d.SetValue("test", new Dictionary<string, object> { ["encoding"] = new ASCIIEncoding() }));
            d.SetValue("test", (byte) 1);
            d.SetValue("test", (sbyte) 1);
            d.SetValue("test", (ushort) 1);
            d.SetValue("test", (short) 1);
            d.SetValue("test", 1);
            d.SetValue("test", 1U);
            d.SetValue("test", 1L);
            d.SetValue("test", 1UL);
            d.SetValue("test", true);
            d.SetValue("test", "Test");
            d.SetValue("test", 1.1f);
            d.SetValue("test", 1.1);
            d.SetValue("test", DateTimeOffset.UtcNow);
            d.SetValue("test", new[] { 1, 2, 3, });
            d.SetValue("test", new Dictionary<string, object> { ["foo"] = "bar" });
            d.SetValue("test", new ArrayObject());
            d.SetValue("test", new MutableArrayObject());
            d.SetValue("test", new DictionaryObject());
            d.SetValue("test", new MutableDictionaryObject());
        }
    }

    [Fact]
    public void TestInMemoryDictionary()
    {
        IDictionary<string, object?> idic = new Dictionary<string, object?>
        {
            { "one", 1 },
            { "two", 2 },
            { "three", 3 },
            { "four", 4 },
            { "five", 5 },
            { "six", 6 }
        };
        var imDict = new InMemoryDictionary(idic);
        imDict.Count.ShouldBe(6);
    }

    [Fact]
    public void TestTypesInDictionaryToJSON()
    {
        var dic = PopulateDictData();
        var md = new MutableDictionaryObject();
        foreach (var item in dic) {
            md.SetValue(item.Key, item.Value); // platform dictionary and list or array will be converted into Couchbase object in SetValue method
        }

        using (var doc = new MutableDocument("doc1")) {
            doc.SetDictionary("dict", md);
            DefaultCollection.Save(doc);
        }

        using (var doc = DefaultCollection.GetDocument("doc1")) {
            var dict = doc?.GetDictionary("dict");
            var json = dict?.ToJSON();
            json.ShouldNotBeNull("because otherwise getting JSON from the object failed");
            ValidateToJsonValues(json, dic);
        }
    }

    [Fact]
    public void TestMutableDictWithJsonString()
    {
        var dic = PopulateDictData();
        var dicJson = JsonSerializer.Serialize(dic, JsonOptions);
        var md = new MutableDictionaryObject(dicJson);

        ValidateValuesInMutableDictFromJson(dic, md);
    }

    [Fact]
    public void TestMutableDictToJsonThrowException()
    {
        var md = new MutableDictionaryObject();
        Should.Throw<NotSupportedException>(md.ToJSON);
    }

    [Fact]
    public void TestMutableDictSetJsonWithInvalidParam()
    {
        var md = new MutableDictionaryObject();
        // with random string 
        var ex = Should.Throw<CouchbaseLiteException>(() => md.SetJSON("random string"));
        ex.Message.ShouldBe(CouchbaseLiteErrorMessage.InvalidJSON);

        //with array json string    
        string[] arr = ["apple", "banana", "orange"];
        var jArr = JsonSerializer.Serialize(arr);
        ex = Should.Throw<CouchbaseLiteException>(() => md.SetJSON(jArr));
        ex.Message.ShouldBe(CouchbaseLiteErrorMessage.InvalidJSON);
    }

    [Fact]
    public void TestCreateMutableDictWithInvalidStr()
    {
        // with random string 
        var ex = Should.Throw<CouchbaseLiteException>(() => new MutableDictionaryObject("random string"));
        ex.Message.ShouldBe(CouchbaseLiteErrorMessage.InvalidJSON);

        //with array json string    
        string[] arr = ["apple", "banana", "orange"];
        var jArr = JsonSerializer.Serialize(arr);
        ex = Should.Throw<CouchbaseLiteException>(() => new MutableDictionaryObject(jArr));
        ex.Message.ShouldBe(CouchbaseLiteErrorMessage.InvalidJSON);
    }
}
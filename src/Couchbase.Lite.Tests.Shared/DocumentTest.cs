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
using System.Text.Json;
using System.Threading;
using Couchbase.Lite;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable AccessToModifiedClosure

#if NET8_0_OR_GREATER
using Test.Util;
// ReSharper disable AccessToDisposedClosure
#endif

namespace Test;

public class DocumentTest(ITestOutputHelper output) : TestCase(output)
{
    private const string BlobStr = "i'm blob";

    [Fact]
    public void TestCreateDoc()
    {
        var doc = new MutableDocument();
        doc.Id.ShouldNotBeEmpty("because every document should have an ID");
        doc.IsDeleted.ShouldBeFalse("because the document is not deleted");
        doc.ToDictionary().ShouldBeEmpty("because the document has no properties");

        SaveDocument(doc);
    }

    [Fact]
    public void TestCreateDocWithID()
    {
        var doc = new MutableDocument("doc1");
        doc.Id.ShouldBe("doc1", "because that was the ID it was given");
        doc.IsDeleted.ShouldBeFalse("because the document is not deleted");
        doc.ToDictionary().ShouldBeEmpty("because the document has no properties");

        SaveDocument(doc);
    }

    [Fact]
    public void TestCreateDocWithEmptyStringID()
    {
        var doc = new MutableDocument("");
        doc.Id.ShouldBeEmpty("because that was the ID it was given");
        doc.IsDeleted.ShouldBeFalse("because the document is not deleted");
        doc.ToDictionary().ShouldBeEmpty("because the document has no properties");

        var ex = Should.Throw<CouchbaseLiteException>(() => DefaultCollection.Save(doc));
        ex.Error.ShouldBe(CouchbaseLiteError.BadDocID);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
    }

    [Fact]
    public void TestCreateDocWithNullID()
    {
        var doc = new MutableDocument(default(string));
        doc.Id.ShouldNotBeEmpty("because every document should have an ID");
        doc.IsDeleted.ShouldBeFalse("because the document is not deleted");
        doc.ToDictionary().ShouldBeEmpty("because the document has no properties");

        SaveDocument(doc);
    }

    [Fact]
    public void TestCreateDocWithDict()
    {
        var dict = new Dictionary<string, object?> {
            ["name"] = "Scott Tiger",
            ["age"] = 30,
            ["address"] = new Dictionary<string, object?> {
                ["street"] = "1 Main Street.",
                ["city"] = "Mountain View",
                ["state"] = "CA"
            },
            ["phones"] = new List<object?> {"650-123-0001", "650-123-0002"}
        };

        var doc = new MutableDocument(dict);
        doc.Id.ShouldNotBeEmpty("because every document should have an ID");
        doc.IsDeleted.ShouldBeFalse("because the document is not deleted");
        doc.ToDictionary().ShouldBeEquivalentToFluent(dict, "because the document was given properties");

        SaveDocument(doc);
    }

    [Fact]
    public void TestCreateDocWithIDAndDict()
    {
        var dict = new Dictionary<string, object?> {
            ["name"] = "Scott Tiger",
            ["age"] = 30,
            ["address"] = new Dictionary<string, object?> {
                ["street"] = "1 Main Street.",
                ["city"] = "Mountain View",
                ["state"] = "CA"
            },
            ["phones"] = new List<object?> { "650-123-0001", "650-123-0002" }
        };

        var doc = new MutableDocument("doc1", dict);
        doc.Id.ShouldBe("doc1", "because that was the ID it was given");
        doc.IsDeleted.ShouldBeFalse("because the document is not deleted");
        doc.ToDictionary().ShouldBeEquivalentToFluent(dict, "because the document was given properties");

        SaveDocument(doc);
    }

    [Fact]
    public void TestSetDictionaryContent()
    {
        var dict = new Dictionary<string, object?> {
            ["name"] = "Scott Tiger",
            ["age"] = 30,
            ["address"] = new Dictionary<string, object?> {
                ["street"] = "1 Main Street.",
                ["city"] = "Mountain View",
                ["state"] = "CA"
            },
            ["phones"] = new List<object?> { "650-123-0001", "650-123-0002" }
        };

        var doc = new MutableDocument("doc1");
        doc.SetData(dict);
        doc.ToDictionary().ShouldBeEquivalentToFluent(dict, "because that is what was just set");

        SaveDocument(doc);

        var nuDict = new Dictionary<string, object?> {
            ["name"] = "Danial Tiger",
            ["age"] = 32,
            ["address"] = new Dictionary<string, object?> {
                ["street"] = "2 Main Street.",
                ["city"] = "Palo Alto",
                ["state"] = "CA"
            },
            ["phones"] = new List<object?> { "650-234-0001", "650-234-0002" }
        };
            
        doc.SetData(nuDict);
        doc.ToDictionary().ShouldBeEquivalentToFluent(nuDict, "because that is what was just set");

        SaveDocument(doc);
    }

    [Fact]
    public void TestGetValueFromDocument()
    {
        var doc = new MutableDocument("doc1");
        SaveDocument(doc, d =>
        {
            d.GetInt("key").ShouldBe(0, "because no integer exists for this key");
            d.GetDouble("key").ShouldBe(0.0, "because no double exists for this key");
            d.GetFloat("key").ShouldBe(0.0f, "because no float exists for this key");
            d.GetBoolean("key").ShouldBeFalse("because no boolean exists for this key");
            d.GetBlob("key").ShouldBeNull("because no blob exists for this key");
            d.GetDate("key").ShouldBe(DateTimeOffset.MinValue, "because no date exists for this key");
            d.GetValue("key").ShouldBeNull("because no object exists for this key");
            d.GetString("key").ShouldBeNull("because no string exists for this key");
            d.GetDictionary("key").ShouldBeNull("because no subdocument exists for this key");
            d.GetArray("key").ShouldBeNull("because no array exists for this key");
            d.ToDictionary().ShouldBeEmpty("because this document has no properties");
        });
    }

    [Fact]
    public void TestMutableDocument()
    {
        var doc = new MutableDocument("doc1");
        SaveDocument(doc, d =>
        {
            d.GetInt("key").ShouldBe(0, "because no integer exists for this key");
            d.GetDouble("key").ShouldBe(0.0, "because no double exists for this key");
            d.GetFloat("key").ShouldBe(0.0f, "because no float exists for this key");
            d.GetBoolean("key").ShouldBeFalse("because no boolean exists for this key");
            d.GetBlob("key").ShouldBeNull("because no blob exists for this key");
            d.GetDate("key").ShouldBe(DateTimeOffset.MinValue, "because no date exists for this key");
            d.GetValue("key").ShouldBeNull("because no object exists for this key");
            d.GetString("key").ShouldBeNull("because no string exists for this key");
            d.GetDictionary("key").ShouldBeNull("because no subdocument exists for this key");
            d.GetArray("key").ShouldBeNull("because no array exists for this key");
            d.ToDictionary().ShouldBeEmpty("because this document has no properties");
        });
    }

    [Fact]
    public void TestSaveThenGetFromAnotherDB()
    {
        var doc = new MutableDocument("doc1");
        doc.SetString("name", "Scott Tiger");

        DefaultCollection.Save(doc);

        using var anotherDb = new Database(Db);
        var doc1B = anotherDb.GetDefaultCollection().GetDocument("doc1");
        doc1B.ShouldNotBeNull("because even from another handle the document should be retrievable");
        doc1B.ShouldNotBeSameAs(doc, "because unique instances should be returned");
        doc.Id.ShouldBe(doc1B.Id, "because object for the same document should have matching IDs");
        doc.ToDictionary().ShouldBeEquivalentToFluent(doc1B.ToDictionary(), "because the contents should match");
    }

    [Fact]
    public void TestNoCacheNoLive()
    {
        var doc1A = new MutableDocument("doc1");
        doc1A.SetString("name", "Scott Tiger");
        SaveDocument(doc1A);

        var doc1B = DefaultCollection.GetDocument("doc1");
        doc1B.ShouldNotBeNull("because the document was just saved");
        var doc1C = DefaultCollection.GetDocument("doc1")!; // Note this is the exact same code, no need to null check again

        using var anotherDb = new Database(Db);
        var doc1D = anotherDb.GetDefaultCollection().GetDocument("doc1");
        doc1D.ShouldNotBeNull("because even from another handle the document should be retrievable");

        doc1A.ShouldNotBeSameAs(doc1B, "because unique instances should be returned");
        doc1A.ShouldNotBeSameAs(doc1C, "because unique instances should be returned");
        doc1A.ShouldNotBeSameAs(doc1D, "because unique instances should be returned");
        doc1B.ShouldNotBeSameAs(doc1C, "because unique instances should be returned");
        doc1B.ShouldNotBeSameAs(doc1D, "because unique instances should be returned");
        doc1C.ShouldNotBeSameAs(doc1D, "because unique instances should be returned");

        doc1A.ToDictionary().ShouldBeEquivalentToFluent(doc1B.ToDictionary(), "because the contents should match");
        doc1A.ToDictionary().ShouldBeEquivalentToFluent(doc1C.ToDictionary(), "because the contents should match");
        doc1A.ToDictionary().ShouldBeEquivalentToFluent(doc1D.ToDictionary(), "because the contents should match");

        var updatedDoc1B = doc1B.ToMutable();
        updatedDoc1B.SetString("name", "Daniel Tiger");
        SaveDocument(updatedDoc1B);

        updatedDoc1B.Equals(doc1A).ShouldBeFalse("because the contents should not match anymore");
        updatedDoc1B.Equals(doc1C).ShouldBeFalse("because the contents should not match anymore");
        updatedDoc1B.Equals(doc1D).ShouldBeFalse("because the contents should not match anymore");
    }

    [Fact]
    public void TestSetString()
    {
        var doc = new MutableDocument("doc1");
        doc.SetString("string1", "");
        doc.SetString("string2", "string");

        SaveDocument(doc, d =>
        {
            d.GetString("string1").ShouldBe("", "because that is the value of the first revision of string1");
            d.GetString("string2")
                .ShouldBe("string", "because that is the value of the first revision of string2");
        });

        doc.Dispose();
        doc = DefaultCollection.GetDocument("doc1")?.ToMutable();
        doc.ShouldNotBeNull("because the document was just saved");
        doc.SetString("string2", "");
        doc.SetString("string1", "string");

        SaveDocument(doc, d =>
        {
            d.GetString("string2").ShouldBe("", "because that is the value of the second revision of string2");
            d.GetString("string1")
                .ShouldBe("string", "because that is the value of the second revision of string1");
        });
    }

    [Fact]
    public void TestGetString()
    {
        var doc = new MutableDocument("doc1");
        PopulateData(doc);
        SaveDocument(doc, d =>
        {
            d.GetString("true").ShouldBeNull("because there is no string in 'true'");
            d.GetString("false").ShouldBeNull("because there is no string in 'false'");
            d.GetString("string").ShouldBe("string", "because there is a string in 'string'");
            d.GetString("zero").ShouldBeNull("because there is no string in 'zero'");
            d.GetString("one").ShouldBeNull("because there is no string in 'one'");
            d.GetString("minus_one").ShouldBeNull("because there is no string in 'minus_one'");
            d.GetString("one_dot_one").ShouldBeNull("because there is no string in 'one_dot_one'");
            d.GetString("date").ShouldBe(d.GetDate("date").ToString("o"), "because date is convertible to string");
            d.GetString("dict").ShouldBeNull("because there is no string in 'dict'");
            d.GetString("array").ShouldBeNull("because there is no string in 'array'");
            d.GetString("blob").ShouldBeNull("because there is no string in 'blob'");
            d.GetString("non_existing_key").ShouldBeNull("because that key has no value");
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
            d.GetInt("number1").ShouldBe(1, "because that is the value of the first revision of number1");
            d.GetInt("number2").ShouldBe(0, "because that is the value of the first revision of number2");
            d.GetInt("number3").ShouldBe(-1, "because that is the value of the first revision of number3");
            d.GetDouble("number4").ShouldBe(1.1, "because that is the value of the first revision of number4");
        });

        doc.Dispose();
        doc = DefaultCollection.GetDocument("doc1")?.ToMutable();
        doc.ShouldNotBeNull("because the document was just saved");
        doc.SetInt("number1", 0);
        doc.SetInt("number2", 1);
        doc.SetDouble("number3", 1.1);
        doc.SetInt("number4", -1);

        SaveDocument(doc, d =>
        {
            d.GetInt("number1").ShouldBe(0, "because that is the value of the second revision of number1");
            d.GetInt("number2").ShouldBe(1, "because that is the value of the second revision of number2");
            d.GetDouble("number3").ShouldBe(1.1, "because that is the value of the second revision of number3");
            d.GetInt("number4").ShouldBe(-1, "because that is the value of the second revision of number4");
        });
    }

    [Fact]
    public void TestGetInteger()
    {
        var doc = new MutableDocument("doc1");
        PopulateData(doc);
        SaveDocument(doc, d =>
        {
            d.GetInt("true").ShouldBe(1, "because a true bool value will be coalesced to 1");
            d.GetInt("false").ShouldBe(0, "because a false bool value will be coalesced to 0");
            d.GetInt("string").ShouldBe(0, "because that is the default value");
            d.GetInt("zero").ShouldBe(0, "because zero was stored in this key");
            d.GetInt("one").ShouldBe(1, "because one was stored in this key");
            d.GetInt("minus_one").ShouldBe(-1, "because -1 was stored in this key");
            d.GetInt("one_dot_one").ShouldBe(1, "because 1.1 gets truncated to 1");
            d.GetInt("date").ShouldBe(0, "because that is the default value");
            d.GetInt("dict").ShouldBe(0, "because that is the default value");
            d.GetInt("array").ShouldBe(0, "because that is the default value");
            d.GetInt("blob").ShouldBe(0, "because that is the default value");
            d.GetInt("non_existing_key").ShouldBe(0, "because that key has no value");
        });
    }

    [Fact]
    public void TestGetLong()
    {
        var doc = new MutableDocument("doc1");
        PopulateData(doc);
        SaveDocument(doc, d =>
        {
            d.GetLong("true").ShouldBe(1L, "because a true bool value will be coalesced to 1L");
            d.GetLong("false").ShouldBe(0L, "because a false bool value will be coalesced to 0L");
            d.GetLong("string").ShouldBe(0L, "because that is the default value");
            d.GetLong("zero").ShouldBe(0L, "because zero was stored in this key");
            d.GetLong("one").ShouldBe(1L, "because one was stored in this key");
            d.GetLong("minus_one").ShouldBe(-1L, "because -1L was stored in this key");
            d.GetLong("one_dot_one").ShouldBe(1L, "because 1L.1L gets truncated to 1L");
            d.GetLong("date").ShouldBe(0L, "because that is the default value");
            d.GetLong("dict").ShouldBe(0L, "because that is the default value");
            d.GetLong("array").ShouldBe(0L, "because that is the default value");
            d.GetLong("blob").ShouldBe(0L, "because that is the default value");
            d.GetLong("non_existing_key").ShouldBe(0L, "because that key has no value");
        });
    }

    [Fact]
    public void TestGetDouble()
    {
        var doc = new MutableDocument("doc1");
        PopulateData(doc);
        SaveDocument(doc, d =>
        {
            d.GetDouble("true").ShouldBe(1.0, "because a true bool value will be coalesced to 1.0");
            d.GetDouble("false").ShouldBe(0.0, "because a false bool value will be coalesced to 0.0");
            d.GetDouble("string").ShouldBe(0.0, "because that is the default value");
            d.GetDouble("zero").ShouldBe(0.0, "because zero was stored in this key");
            d.GetDouble("one").ShouldBe(1.0, "because one was stored in this key");
            d.GetDouble("minus_one").ShouldBe(-1.0, "because -1 was stored in this key");
            d.GetDouble("one_dot_one").ShouldBe(1.1, "because 1.1 was stored in this key");
            d.GetDouble("date").ShouldBe(0.0, "because that is the default value");
            d.GetDouble("dict").ShouldBe(0.0, "because that is the default value");
            d.GetDouble("array").ShouldBe(0.0, "because that is the default value");
            d.GetDouble("blob").ShouldBe(0.0, "because that is the default value");
            d.GetDouble("non_existing_key").ShouldBe(0.0, "because that key has no value");
        });
    }

    [Fact]
    public void TestGetFloat()
    {
        var doc = new MutableDocument("doc1");
        PopulateData(doc);
        SaveDocument(doc, d =>
        {
            d.GetFloat("true").ShouldBe(1.0f, "because a true bool value will be coalesced to 1.0f");
            d.GetFloat("false").ShouldBe(0.0f, "because a false bool value will be coalesced to 0.0f");
            d.GetFloat("string").ShouldBe(0.0f, "because that is the default value");
            d.GetFloat("zero").ShouldBe(0.0f, "because zero was stored in this key");
            d.GetFloat("one").ShouldBe(1.0f, "because one was stored in this key");
            d.GetFloat("minus_one").ShouldBe(-1.0f, "because -1 was stored in this key");
            d.GetFloat("one_dot_one").ShouldBe(1.1f, "because 1.1f was stored in this key");
            d.GetFloat("date").ShouldBe(0.0f, "because that is the default value");
            d.GetFloat("dict").ShouldBe(0.0f, "because that is the default value");
            d.GetFloat("array").ShouldBe(0.0f, "because that is the default value");
            d.GetFloat("blob").ShouldBe(0.0f, "because that is the default value");
            d.GetFloat("non_existing_key").ShouldBe(0.0f, "because that key has no value");
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
            d.GetLong("min_int").ShouldBe(Int64.MinValue, "because that is what was stored");
            d.GetLong("max_int").ShouldBe(Int64.MaxValue, "because that is what was stored");
            d.GetDouble("min_double").ShouldBe(Double.MinValue, "because that is what was stored");
            d.GetDouble("max_double").ShouldBe(Double.MaxValue, "because that is what was stored");
            d.GetFloat("min_float").ShouldBe(Single.MinValue, "because that is what was stored");
            d.GetFloat("max_float").ShouldBe(Single.MaxValue, "because that is what was stored");
        });
    }

    [Fact]
    public void TestSetGetFloatNumbers()
    {
        using var doc = new MutableDocument("doc1");
        doc.SetFloat("number1", 1.00f)
            .SetFloat("number2", 1.49f)
            .SetFloat("number3", 1.50f)
            .SetFloat("number4", 1.51f)
            .SetDouble("number5", 1.99);

        SaveDocument(doc, d =>
        {
            d.GetInt("number1").ShouldBe(1);
            d.GetFloat("number1").ShouldBe(1.00f);
            d.GetDouble("number1").ShouldBe(1.00);

            d.GetInt("number2").ShouldBe(1);
            d.GetFloat("number2").ShouldBe(1.49f);
            d.GetDouble("number2").ShouldBe(1.49, 0.00001);

            d.GetInt("number3").ShouldBe(1);
            d.GetFloat("number3").ShouldBe(1.50f);
            d.GetDouble("number3").ShouldBe(1.50, 0.00001);

            d.GetInt("number4").ShouldBe(1);
            d.GetFloat("number4").ShouldBe(1.51f);
            d.GetDouble("number4").ShouldBe(1.51, 0.00001);

            d.GetInt("number5").ShouldBe(1);
            d.GetFloat("number5").ShouldBe(1.99f);
            d.GetDouble("number5").ShouldBe(1.99, 0.00001);
        });
    }

    [Fact]
    public void TestSetBoolean()
    {
        var doc = new MutableDocument("doc1");
        doc.SetBoolean("boolean1", true);
        doc.SetBoolean("boolean2", false);

        SaveDocument(doc, d =>
        {
            d.GetBoolean("boolean1").ShouldBe(true, "because that is the value of the first revision of boolean1");
            d.GetBoolean("boolean2").ShouldBe(false, "because that is the value of the first revision of boolean2");
        });

        doc.Dispose();
        doc = DefaultCollection.GetDocument("doc1")?.ToMutable();
        doc.ShouldNotBeNull("because the document was just saved");
        doc.SetBoolean("boolean1", false);
        doc.SetBoolean("boolean2", true);

        SaveDocument(doc, d =>
        {
            d.GetBoolean("boolean1").ShouldBe(false, "because that is the value of the second revision of boolean1");
            d.GetBoolean("boolean2").ShouldBe(true, "because that is the value of the second revision of boolean2");
        });
    }

    [Fact]
    public void TestGetBoolean()
    {
        var doc = new MutableDocument("doc1");
        PopulateData(doc);
        SaveDocument(doc, d =>
        {
            d.GetBoolean("true").ShouldBe(true, "because true was stored");
            d.GetBoolean("false").ShouldBe(false, "because false was stored");
            d.GetBoolean("string").ShouldBe(true, "because any non-zero object will be true");
            d.GetBoolean("zero").ShouldBe(false, "because zero will coalesce to false");
            d.GetBoolean("one").ShouldBe(true, "because any non-zero object will be true");
            d.GetBoolean("minus_one").ShouldBe(true, "because any non-zero object will be true");
            d.GetBoolean("one_dot_one").ShouldBe(true, "because any non-zero object will be true");
            d.GetBoolean("date").ShouldBe(true, "because any non-zero object will be true");
            d.GetBoolean("dict").ShouldBe(true, "because any non-zero object will be true");
            d.GetBoolean("array").ShouldBe(true, "because any non-zero object will be true");
            d.GetBoolean("blob").ShouldBe(true, "because any non-zero object will be true");
            d.GetBoolean("non_existing_key").ShouldBe(false, "because that key has no value");
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
            d.GetValue("date").ShouldBe(dateStr, "because that is what was stored");
            d.GetString("date").ShouldBe(dateStr, "because a string was stored");
            d.GetDate("date").ShouldBe(date, "because the string is convertible to a date");
        });

        doc.Dispose();
        doc = DefaultCollection.GetDocument("doc1")?.ToMutable();
        doc.ShouldNotBeNull("because the document was just saved");
        var nuDate = date + TimeSpan.FromSeconds(60);
        var nuDateStr = nuDate.ToString("o");
        doc.SetDate("date", nuDate);
            
        SaveDocument(doc, d =>
        {
            d.GetDate("date").ShouldBe(nuDate, "because that is what was stored the second time");
            d.GetString("date").ShouldBe(nuDateStr, "because the date is convertible to a string");
        });
    }

    [Fact]
    public void TestGetDate()
    {
        var doc = new MutableDocument("doc1");
        PopulateData(doc);
        SaveDocument(doc, d =>
        {
            d.GetDate("true").ShouldBe(DateTimeOffset.MinValue, "because that is the default");
            d.GetDate("false").ShouldBe(DateTimeOffset.MinValue, "because that is the default");
            d.GetDate("string").ShouldBe(DateTimeOffset.MinValue, "because that is the default");
            d.GetDate("zero").ShouldBe(DateTimeOffset.MinValue, "because that is the default");
            d.GetDate("one").ShouldBe(DateTimeOffset.MinValue, "because that is the default");
            d.GetDate("minus_one").ShouldBe(DateTimeOffset.MinValue, "because that is the default");
            d.GetDate("one_dot_one").ShouldBe(DateTimeOffset.MinValue, "because that is the default");
            d.GetDate("date").ToString("o").ShouldBe(d.GetString("date"), "because the date and its string should match");
            d.GetDate("dict").ShouldBe(DateTimeOffset.MinValue, "because that is the default");
            d.GetDate("array").ShouldBe(DateTimeOffset.MinValue, "because that is the default");
            d.GetDate("blob").ShouldBe(DateTimeOffset.MinValue, "because that is the default");
            d.GetDate("non_existing_key").ShouldBe(DateTimeOffset.MinValue, "because that key has no value");
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
            (d.GetValue("blob") as Blob)?
                .Properties.ShouldBeEquivalentToFluent(blob.Properties,
                    "because otherwise the blob did not store correctly");
            d.GetBlob("blob")?
                .Properties.ShouldBeEquivalentToFluent(blob.Properties,
                    "because otherwise the blob did not store correctly");
            d.GetBlob("blob")?
                .Content.ShouldBeEquivalentToFluent(blob.Content,
                    "because otherwise the blob did not store correctly");
        });

        doc.Dispose();
        doc = DefaultCollection.GetDocument("doc1")?.ToMutable();
        doc.ShouldNotBeNull("because the document was just saved");
        var nuContent = "1234567890"u8.ToArray();
        var nuBlob = new Blob("text/plain", nuContent);
        doc.SetBlob("blob", nuBlob);

        SaveDocument(doc, d =>
        {
            (d.GetValue("blob") as Blob)?
                .Properties.ShouldBeEquivalentToFluent(nuBlob.Properties,
                    "because otherwise the blob did not update correctly");
            d.GetBlob("blob")?
                .Properties.ShouldBeEquivalentToFluent(nuBlob.Properties,
                    "because otherwise the blob did not update correctly");
            d.GetBlob("blob")?
                .Content.ShouldBeEquivalentToFluent(nuBlob.Content,
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
            d.GetBlob("true").ShouldBeNull("because that is the default");
            d.GetBlob("false").ShouldBeNull("because that is the default");
            d.GetBlob("string").ShouldBeNull("because that is the default");
            d.GetBlob("zero").ShouldBeNull("because that is the default");
            d.GetBlob("one").ShouldBeNull("because that is the default");
            d.GetBlob("minus_one").ShouldBeNull("because that is the default");
            d.GetBlob("one_dot_one").ShouldBeNull("because that is the default");
            d.GetBlob("date").ShouldBeNull("because that is the default");
            d.GetBlob("dict").ShouldBeNull("because that is the default");
            d.GetBlob("array").ShouldBeNull("because that is the default");
            var b = d.GetBlob("blob");
            b.ShouldNotBeNull();
            b.Content.ShouldBe(Encoding.UTF8.GetBytes("12345"),
                "because that is the content that was stored");
            d.GetBlob("non_existing_key").ShouldBeNull("because that key has no value");
        });
    }

    [Fact]
    public void TestSetDictionary()
    {
        var doc = new MutableDocument("doc1");
        var dict = new MutableDictionaryObject();
        dict.SetString("street", "1 Main street");
        doc.SetDictionary("dict", dict);
        doc.GetValue("dict").ShouldBe(dict, "because that is what was stored");
        SaveDocument(doc, d => { d.GetDictionary("dict").ShouldBeEquivalentToFluent(dict); });

        dict = doc.GetDictionary("dict");
        dict?.SetString("city", "Mountain View");
        SaveDocument(doc, d => { d.GetDictionary("dict").ShouldBeEquivalentToFluent(dict); });
    }

    [Fact]
    public void TestGetDictionary()
    {
        var doc = new MutableDocument("doc1");
        PopulateData(doc);
        SaveDocument(doc, d =>
        {
            d.GetDictionary("true").ShouldBeNull("because that is the default");
            d.GetDictionary("false").ShouldBeNull("because that is the default");
            d.GetDictionary("string").ShouldBeNull("because that is the default");
            d.GetDictionary("zero").ShouldBeNull("because that is the default");
            d.GetDictionary("one").ShouldBeNull("because that is the default");
            d.GetDictionary("minus_one").ShouldBeNull("because that is the default");
            d.GetDictionary("one_dot_one").ShouldBeNull("because that is the default");
            d.GetDictionary("date").ShouldBeNull("because that is the default");
            var csharpDict = new Dictionary<string, object> {
                ["street"] = "1 Main street",
                ["city"] = "Mountain View",
                ["state"] = "CA"
            };
            var dict = d.GetDictionary("dict");
            dict.ShouldNotBeNull();
            dict.ToDictionary().ShouldBeEquivalentToFluent(csharpDict, 
                "because those are the stored contents");
            d.GetDictionary("array").ShouldBeNull("because that is the default");
            d.GetDictionary("blob").ShouldBeNull("because that is the default");
            d.GetDictionary("non_existing_key").ShouldBeNull("because that key has no value");
        });
    }

    [Fact]
    public void TestSetArray()
    {
        var doc = new MutableDocument("doc1");
        var array = new MutableArrayObject();
        array.AddString("item1").AddString("item2").AddString("item3");

        doc.SetArray("array", array);

        doc.GetValue("array").ShouldBe(array, "because that is what was stored");
        doc.GetArray("array").ShouldBe(array, "because that is what was stored");
        doc.GetArray("array")
            .ShouldBeEquivalentToFluent(new[] {"item1", "item2", "item3"}, 
                "because otherwise the contents are incorrect");

        SaveDocument(doc, d => { d.GetArray("array")?.ToList().ShouldBe(array.ToList()); });

        array = doc.GetArray("array");
        array.ShouldNotBeNull("because it was saved into the document");
        array.AddString("item4");
        array.AddString("item5");
        SaveDocument(doc, d => { d.GetArray("array")?.ToList().ShouldBe(array.ToList()); });
    }

    [Fact]
    public void TestGetArray()
    {
        var doc = new MutableDocument("doc1");
        PopulateData(doc);
        SaveDocument(doc, d =>
        {
            d.GetArray("true").ShouldBeNull("because that is the default");
            d.GetArray("false").ShouldBeNull("because that is the default");
            d.GetArray("string").ShouldBeNull("because that is the default");
            d.GetArray("zero").ShouldBeNull("because that is the default");
            d.GetArray("one").ShouldBeNull("because that is the default");
            d.GetArray("minus_one").ShouldBeNull("because that is the default");
            d.GetArray("one_dot_one").ShouldBeNull("because that is the default");
            d.GetArray("date").ShouldBeNull("because that is the default");
            d.GetArray("dict").ShouldBeNull("because that is the default");
            var arr = d.GetArray("array");
            arr.ShouldNotBeNull();
            arr.ShouldBeEquivalentToFluent(new[] { "650-123-0001", "650-123-0002" },
                "because that is the array that was stored");
            d.GetArray("blob").ShouldBeNull("because that is the default");
            d.GetArray("non_existing_key").ShouldBeNull("because that key has no value");
        });
    }

    [Fact]
    public void TestSetNull()
    {
        var doc = new MutableDocument("doc1");
        doc.SetValue("null", null);
        SaveDocument(doc, d =>
        {
            d.GetValue("null").ShouldBeNull("because that is what was stored");
            d.Count.ShouldBe(1, "because the value is null, not missing");
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
        address.ShouldBeSameAs(doc.GetValue("address"), "because the same doc should return the same object");
        address!.GetString("street").ShouldBe("1 Main street", "because that is the street that was stored");
        address.GetString("city").ShouldBe("Mountain View", "because that is the city that was stored");
        address.GetString("state").ShouldBe("CA", "because that is the state that was stored");
        address.ToDictionary().ShouldBeEquivalentToFluent(dict, "because the content should be the same");

        var nuDict = new Dictionary<string, object> {
            ["street"] = "1 Second street",
            ["city"] = "Palo Alto",
            ["state"] = "CA"
        };
        doc.SetValue("address", nuDict);

        // Make sure the old address dictionary is not affected
        address.ShouldNotBeSameAs(doc.GetDictionary("address"), "because address is now detached");
        address.GetString("street").ShouldBe("1 Main street", "because that is the street that was stored");
        address.GetString("city").ShouldBe("Mountain View", "because that is the city that was stored");
        address.GetString("state").ShouldBe("CA", "because that is the state that was stored");
        address.ToDictionary().ShouldBeEquivalentToFluent(dict, "because the content should be the same");
        var nuAddress = doc.GetDictionary("address");
        nuAddress.ShouldNotBeNull("because it was written into the document");
        nuAddress.ShouldNotBeSameAs(address, "because they are two different entities");

        nuAddress.SetString("zip", "94302");
        nuAddress.GetString("zip").ShouldBe("94302", "because that was what was just stored");
        address.GetString("zip").ShouldBeNull("because address should not be affected");

        nuDict["zip"] = "94302";
        SaveDocument(doc, d => { d.GetDictionary("address").ShouldBeEquivalentToFluent(nuDict); });
    }

    [Fact]
    public void TestSetCSharpList()
    {
        var array = new[] {"a", "b", "c"};
        var doc = new MutableDocument("doc1");
        doc.SetValue("members", array);

        var members = doc.GetArray("members");
        members.ShouldNotBeNull();
        members
            .ShouldBeSameAs(doc.GetValue("members"), "because the same document should return the same object");

        members.Count.ShouldBe(3, "because there are three elements inside");
        members.ShouldBeEquivalentToFluent(array, "because otherwise the contents are wrong");
        members.ToArray().ShouldBeEquivalentToFluent(array, "because otherwise the contents are wrong");

        var nuArray = new[] {"d", "e", "f"};
        doc.SetValue("members", nuArray);

        // Make sure the old members array is not affected
        members.Count.ShouldBe(3, "because there are three elements inside");
        members.ShouldBeEquivalentToFluent(array, "because otherwise the contents are wrong");
        members.ToArray().ShouldBeEquivalentToFluent(array, "because otherwise the contents are wrong");

        var nuMembers = doc.GetArray("members");
        nuMembers.ShouldNotBeNull("because the array was written into the document");
        members.ShouldNotBeSameAs(nuMembers, "because the new array should have no relation to the old");
        nuMembers.AddString("g");
        nuMembers.Count.ShouldBe(4, "because another element was added");
        nuMembers.GetValue(3).ShouldBe("g", "because that is what was added");
        members.Count.ShouldBe(3, "because members still has three elements");

        SaveDocument(doc, d =>
        {
            d.ToDictionary()
                .ShouldBeEquivalentToFluent(new Dictionary<string, object> { ["members"] = (string[])["d", "e", "f", "g" ]
                    }, "because otherwise the document contents are incorrect");
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
            d.ToDictionary().ShouldBeEquivalentToFluent(new Dictionary<string, object>
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

        var gotShipping = doc.GetDictionary("addresses")?.GetDictionary("shipping");
        gotShipping.ShouldNotBeNull("because it was saved into the document");
        gotShipping.SetString("zip", "94042");

        SaveDocument(doc, d =>
        {
            d.ToDictionary().ShouldBeEquivalentToFluent(new Dictionary<string, object>
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
            d.ToDictionary().ShouldBeEquivalentToFluent(result);
        });

        address1 = doc.GetArray("addresses")?.GetDictionary(0);
        address1.ShouldNotBeNull("because address1 was just created");
        address1.SetString("zip", "94042");

        address2 = doc.GetArray("addresses")?.GetDictionary(1);
        address2.ShouldNotBeNull("because address2 was just created");
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
            d.ToDictionary().ShouldBeEquivalentToFluent(result);
        });
            
        address1 = doc.GetArray("addresses")?.GetDictionary(0);
        address1.ShouldNotBeNull("because address1 was just saved");
        address1.SetString("street", "2 Main street");

        address2 = doc.GetArray("addresses")?.GetDictionary(1);
        address2.ShouldNotBeNull("because address1 was just created");
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
            d.ToDictionary().ShouldBeEquivalentToFluent(result);
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
            d.ToDictionary().ShouldBeEquivalentToFluent(result);
        });

        group1 = doc.GetArray("groups")?.GetArray(0);
        group1.ShouldNotBeNull("because group1 was just added");
        group1.SetString(0, "d");
        group1.SetString(1, "e");
        group1.SetString(2, "f");

        group2 = doc.GetArray("groups")?.GetArray(1);
        group2.ShouldNotBeNull("because group2 was just added");
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
            d.ToDictionary().ShouldBeEquivalentToFluent(result);
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
            d.ToDictionary().ShouldBeEquivalentToFluent(result);
        });

        member1 = doc.GetDictionary("group1")?.GetArray("member");
        member1.ShouldNotBeNull("because member was just added to group1");
        member1.SetString(0, "d");
        member1.SetString(1, "e");
        member1.SetString(2, "f");

        member2 = doc.GetDictionary("group2")?.GetArray("member");
        member2.ShouldNotBeNull("because member was just added to group2");
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
            d.ToDictionary().ShouldBeEquivalentToFluent(result);
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

        doc.GetValue("shipping").ShouldBeSameAs(address, "because that is the object that was stored");
        doc.GetValue("billing").ShouldBeSameAs(address, "because that is the object that was stored");
            
        address.SetString("zip", "94042");
        doc.GetDictionary("shipping")?
            .GetString("zip")
            .ShouldBe("94042", "because the update should be received by both dictionaries");
        doc.GetDictionary("billing")?
            .GetString("zip")
            .ShouldBe("94042", "because the update should be received by both dictionaries");

        SaveDocument(doc);

        DictionaryObject? shipping = doc.GetDictionary("shipping");
        DictionaryObject? billing = doc.GetDictionary("billing");
        shipping.ShouldBeSameAs(address, "because the dictionaries should remain the same until the save");
        billing.ShouldBeSameAs(address, "because the dictionaries should remain the same until the save");

        using var savedDoc = DefaultCollection.GetDocument(doc.Id);
        savedDoc.ShouldNotBeNull("because otherwise the save failed");
        shipping = savedDoc.GetDictionary("shipping");
        billing = savedDoc.GetDictionary("billing");
                
        shipping.ShouldNotBeSameAs(address, "because the dictionaries should now be independent");
        billing.ShouldNotBeSameAs(address, "because the dictionaries should now be independent");
        shipping.ShouldNotBeSameAs(billing, "because the dictionaries should now be independent");
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
            .ShouldBeEquivalentToFluent(new[] { "650-000-0001", "650-000-0002", "650-000-0003" },
                "because both arrays should receive the update");
        doc.GetArray("home")
            .ShouldBeEquivalentToFluent(new[] { "650-000-0001", "650-000-0002", "650-000-0003" },
                "because both arrays should receive the update");

        SaveDocument(doc);

        // Both mobile and home are still the same instance
        ArrayObject? mobile = doc.GetArray("mobile");
        ArrayObject? home = doc.GetArray("home");
        mobile.ShouldBeSameAs(phones, "because all the arrays should still be the same");
        home.ShouldBeSameAs(phones, "because all the arrays should still be the same");

        using var savedDoc = DefaultCollection.GetDocument(doc.Id);
        savedDoc.ShouldNotBeNull("because otherwise the save failed");
        mobile = savedDoc.GetArray("mobile");
        home = savedDoc.GetArray("home");
        mobile.ShouldNotBeSameAs(phones, "because after save the arrays should be independent");
        home.ShouldNotBeSameAs(phones, "because after save the arrays should be independent");
        mobile.ShouldNotBeSameAs(home, "because after save the arrays should be independent");
    }

    [Fact]
    public void TestGetArrayAfterDbSave()
    {
        using (var doc = new MutableDocument("doc1")) {
            var phones = new MutableArrayObject();
            phones.AddString("650-000-0001").AddString("650-000-0002");
            doc.SetArray("mobile", phones);
            SaveDocument(doc);
        }

        using (var doc1 = DefaultCollection.GetDocument("doc1"))
        using (var mDoc1 = doc1?.ToMutable()) {
            mDoc1.ShouldNotBeNull("because otherwise the save failed");
            var phones1 = mDoc1.GetArray("mobile");
            phones1.ShouldNotBeNull("because otherwise the save didn't persist properly");
            for (int i = 0; i < phones1.Count; i++) {
                if (i == 0)
                    phones1[i].ToString().ShouldBe("650-000-0001");
                if (i == 1)
                    phones1[i].ToString().ShouldBe("650-000-0002");
            }

            phones1.AddString("650-000-0003");
            phones1.GetString(0).ShouldBe("650-000-0001");
            phones1.GetString(1).ShouldBe("650-000-0002");
            SaveDocument(mDoc1);
        }

        using (var doc2 = DefaultCollection.GetDocument("doc1"))
        using (var mDoc2 = doc2?.ToMutable()) {
            mDoc2.ShouldNotBeNull("because otherwise the save failed");
            mDoc2.GetArray("mobile")
                .ShouldBeEquivalentToFluent(new[] { "650-000-0001", "650-000-0002", "650-000-0003" },
                    "because both arrays should receive the update");
        }
    }

    [Fact]
    public void TestCount()
    {
        var doc = new MutableDocument("doc1");
        PopulateData(doc);

        SaveDocument(doc, d =>
        {
            d.Count.ShouldBe(11, "because that is the number of entries that were added");
            d.Count.ShouldBe(doc.ToDictionary().Count, 
                "because the count should not change when converting to dictionary");
        });
    }

    [Fact]
    public void TestRemoveKeys()
    {
        var doc = new MutableDocument("doc1");
        doc.SetData(new Dictionary<string, object?> {
            ["type"] = "profile",
            ["name"] = "Jason",
            ["weight"] = 130.5,
            ["address"] = new Dictionary<string, object?> {
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
        doc.GetDictionary("address")?.Remove("city");

        doc.GetString("name").ShouldBeNull("because it was removed");
        doc.GetDouble("weight").ShouldBe(0.0, "because it was removed");
        doc.GetFloat("weight").ShouldBe(0.0f, "because it was removed");
        doc.GetLong("age").ShouldBe(0L, "because it was removed");
        doc.GetBoolean("active").ShouldBeFalse("because it was removed");

        doc.GetValue("name").ShouldBeNull("because it was removed");
        doc.GetValue("weight").ShouldBeNull("because it was removed");
        doc.GetValue("age").ShouldBeNull("because it was removed");
        doc.GetValue("active").ShouldBeNull("because it was removed");
        doc.GetDictionary("address")?.GetString("city").ShouldBeNull("because it was removed");

        doc.Contains("name").ShouldBeFalse("because that key was removed");
        doc.Contains("weight").ShouldBeFalse("because that key was removed");
        doc.Contains("age").ShouldBeFalse("because that key was removed");
        doc.Contains("active").ShouldBeFalse("because that key was removed");
        doc.GetDictionary("address")?.Contains("city").ShouldBeFalse("because that key was removed");

        var address = doc.GetDictionary("address");
        address.ShouldNotBeNull("because the address dictionary should still be present");
        doc.ToDictionary().ShouldBeEquivalentToFluent(new Dictionary<string, object?> {
            ["type"] = "profile",
            ["address"] = new Dictionary<string, object> {
                ["street"] = "1 milky way.",
                ["zip"] = 12345L
            }
        });
        address.ToDictionary().ShouldBeEquivalentToFluent(new Dictionary<string, object?> {
            ["street"] = "1 milky way.",
            ["zip"] = 12345L
        });

        // Remove the rest:
        doc.Remove("type");
        doc.Remove("address");
        doc.GetValue("type").ShouldBeNull("because it was removed");
        doc.GetValue("address").ShouldBeNull("because it was removed");
        doc.ToDictionary().ShouldBeEmpty("because everything was removed");
    }

    [Fact]
    public void TestRemoveKeysBySettingDictionary()
    {
        var props = new Dictionary<string, object?> {
            ["PropName1"] = "Val1",
            ["PropName2"] = 42
        };

        var newDoc = new MutableDocument("docName", props);
        DefaultCollection.Save(newDoc);

        var newProps = new Dictionary<string, object?> {
            ["PropName3"] = "Val3",
            ["PropName4"] = 84
        };

        var existingDoc = DefaultCollection.GetDocument("docName")?.ToMutable();
        existingDoc.ShouldNotBeNull("because otherwise the save failed");
        existingDoc.SetData(newProps);
        DefaultCollection.Save(existingDoc);

        existingDoc.ToDictionary().ShouldBeEquivalentToFluent(new Dictionary<string, object?> {
            ["PropName3"] = "Val3",
            ["PropName4"] = 84
        });
    }

    [Fact]
    public void TestContainsKey()
    {
        var doc = new MutableDocument("doc1");
        doc.SetData(new Dictionary<string, object?> {
            ["type"] = "profile",
            ["name"] = "Jason",
            ["age"] = 30,
            ["address"] = new Dictionary<string, object?> {
                ["street"] = "1 milky way."
            }
        });

        doc.Contains("type").ShouldBeTrue("because 'type' exists in the document");
        doc.Contains("name").ShouldBeTrue("because 'name' exists in the document");
        doc.Contains("address").ShouldBeTrue("because 'address' exists in the document");
        doc.Contains("weight").ShouldBeFalse("because 'weight' does not exist in the document");
    }

    [Fact]
    public void TestDeleteNewDocument()
    {
        var doc = new MutableDocument("doc1");
        doc.SetString("name", "Scott Tiger");
        doc.IsDeleted.ShouldBeFalse("because the document is not deleted");

        var ex = Should.Throw<CouchbaseLiteException>(() => DefaultCollection.Delete(doc),
            "because deleted a non-existent document is invalid");
        ex.Error.ShouldBe(CouchbaseLiteError.NotFound);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        doc.IsDeleted.ShouldBeFalse("because the document is still not deleted");
        doc.GetString("name").ShouldBe("Scott Tiger", "because the delete was invalid");
    }

    [Fact]
    public void TestDeleteDocument()
    {
        var doc1 = new MutableDocument("doc1");
        doc1.SetString("name", "Scott Tiger");
        SaveDocument(doc1);

        DefaultCollection.Delete(doc1);
        DefaultCollection.GetDocument(doc1.Id).ShouldBeNull();
    }

    [Fact]
    public void TestDictionaryAfterDeleteDocument()
    {
        var dict = new Dictionary<string, object?> {
            ["address"] = new Dictionary<string, object?> { 
                ["street"] = "1 Main street",
                ["city"] = "Mountain View",
                ["state"] = "CA"
            }
        };

        var doc = new MutableDocument("doc1", dict);
        SaveDocument(doc);

        using var savedDoc = DefaultCollection.GetDocument(doc.Id);
        savedDoc.ShouldNotBeNull("because otherwise the save failed");
        var address = savedDoc.GetDictionary("address");
        address.ShouldNotBeNull("because otherwise the save didn't persist properly");
        address.GetString("street").ShouldBe("1 Main street", "because that is the street that was stored");
        address.GetString("city").ShouldBe("Mountain View", "because that is the city that was stored");
        address.GetString("state").ShouldBe("CA", "because that is the state that was stored");

        DefaultCollection.Delete(savedDoc);

        address.GetString("street").ShouldBe("1 Main street", "because the dictionary is independent");
        address.GetString("city").ShouldBe("Mountain View", "because the dictionary is independent");
        address.GetString("state").ShouldBe("CA", "because the dictionary is independent");
    }

    [Fact]
    public void TestArrayAfterDeleteDocument()
    {
        var dict = new Dictionary<string, object?> {
            ["members"] = new[] {"a", "b", "c"}
        };

        var doc = new MutableDocument("doc1", dict);
        SaveDocument(doc);

        using var savedDoc = DefaultCollection.GetDocument(doc.Id);
        savedDoc.ShouldNotBeNull("because otherwise the save failed");
        var members = savedDoc.GetArray("members");
        members.ShouldNotBeNull();
        members.Count.ShouldBe(3, "because three elements were added");
        members.ShouldBeEquivalentToFluent(dict["members"], "because otherwise the array has incorrect elements");

        DefaultCollection.Delete(savedDoc);

        members.Count.ShouldBe(3, "because the array is independent of the document");
        members.ShouldBeEquivalentToFluent(dict["members"], "because the array is independent of the document");
    }

    [Fact]
    public void TestPurgeDocument()
    {
        var doc = new MutableDocument("doc1");
        doc.SetString("type", "profile");
        doc.SetString("name", "Scott");
        doc.IsDeleted.ShouldBeFalse("because the document is not deleted");

        var ex = Should.Throw<CouchbaseLiteException>(() => DefaultCollection.Purge(doc));
        ex.Error.ShouldBe(CouchbaseLiteError.NotFound);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);

        // Save:
        SaveDocument(doc);

        Should.NotThrow(() => DefaultCollection.Purge(doc));
    }

    [Fact]
    public void TestPurgeDocumentById()
    {
        var doc = new MutableDocument("doc1");
        doc.SetString("type", "profile");
        doc.SetString("name", "Scott");
        doc.IsDeleted.ShouldBeFalse("because the document is not deleted");

        var ex = Should.Throw<CouchbaseLiteException>(() => DefaultCollection.Purge("doc1"));
        ex.Error.ShouldBe(CouchbaseLiteError.NotFound);
        ex.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);

        // Save:
        SaveDocument(doc);

        DefaultCollection.Purge("doc1");
        DefaultCollection.GetDocument("doc1").ShouldBeNull();
    }

    [Fact]
    public void TestReopenDB()
    {
        var doc = new MutableDocument("doc1");
        doc.SetString("string", "str");
        DefaultCollection.Save(doc);

        ReopenDB();

        var gotDoc = DefaultCollection.GetDocument("doc1");
        gotDoc?.ToDictionary().ShouldBeEquivalentToFluent(new Dictionary<string, object?> { ["string"] = "str" }, "because otherwise the property didn't get saved");
        gotDoc?["string"].ToString().ShouldBe("str", "because otherwise the property didn't get saved");
    }

    [Fact]
    public void TestBlob()
    {
        var content = Encoding.UTF8.GetBytes("12345");
        var data = new Blob("text/plain", content);
        var doc = new MutableDocument("doc1");
        doc.SetBlob("data", data);
        doc.SetString("name", "Jim");
        DefaultCollection.Save(doc);

        using(var otherDb = new Database(Db.Name, Db.Config)) {
            var doc1 = otherDb.GetDefaultCollection().GetDocument("doc1");
            doc1.ShouldNotBeNull("because the document should be accessible from another handle");
            doc1["name"].ToString().ShouldBe("Jim", "because the document should be persistent after save");
            doc1["data"].Value.ShouldBeAssignableTo<Blob>("because otherwise the data did not save correctly");
            data = doc1.GetBlob("data");

            data.ShouldNotBeNull("because otherwise the save didn't persist properly");
            data.Length.ShouldBe(5, "because the data is 5 bytes long");
            data.Content.ShouldBe(content, "because the data should have been retrieved correctly");
            var contentStream = data.ContentStream;
            contentStream.ShouldNotBeNull("because the blob contents should be readable");
            var buffer = new byte[10];
            var bytesRead = contentStream.Read(buffer, 0, 10);
            contentStream.Dispose();
            bytesRead.ShouldBe(5, "because the data is 5 bytes long");
        }

        var stream = new MemoryStream(content);
        data = new Blob("text/plain", stream);
        data.Content.ShouldNotBeNull("because the content is in memory");
        data.Content!.SequenceEqual(content).ShouldBeTrue();
    }

    [Fact]
    public void TestEmptyBlob()
    {
        var content = Array.Empty<byte>();
        var data = new Blob("text/plain", content);
        var doc = new MutableDocument("doc1");
        doc.SetBlob("data", data);
        DefaultCollection.Save(doc);

        using var otherDb = new Database(Db.Name, Db.Config);
        var doc1 = otherDb.GetDefaultCollection().GetDocument("doc1");
        doc1.ShouldNotBeNull("because the document should be accessible from another handle");
        doc1["data"].Value.ShouldBeAssignableTo<Blob>("because otherwise the data did not save correctly");
        data = doc1.GetBlob("data");

        data.ShouldNotBeNull("because otherwise the save didn't persist properly");
        data.Length.ShouldBe(0, "because the data is 5 bytes long");
        data.Content.ShouldBe(content, "because the data should have been retrieved correctly");
        var contentStream = data.ContentStream;
        contentStream.ShouldNotBeNull("because the blob should be readable");
        var buffer = new byte[10];
        var bytesRead = contentStream.Read(buffer, 0, 10);
        contentStream.Dispose();
        bytesRead.ShouldBe(0, "because the data is 5 bytes long");
    }

    [Fact]
    public void TestBlobWithStream()
    {
        var content = Array.Empty<byte>();
        Stream? contentStream = new MemoryStream(content);
        var data = new Blob("text/plain", contentStream);
        var doc = new MutableDocument("doc1");
        doc.SetBlob("data", data);
        DefaultCollection.Save(doc);

        using var otherDb = new Database(Db.Name, Db.Config);
        var doc1 = otherDb.GetDefaultCollection().GetDocument("doc1");
        doc1?["data"].Value.ShouldBeAssignableTo<Blob>("because otherwise the data did not save correctly");
        data = doc1?.GetBlob("data");

        data?.Length.ShouldBe(0, "because the data is 0 bytes long");
        data?.Content.ShouldBe(content, "because the data should have been retrieved correctly");
        contentStream = data?.ContentStream;
        contentStream.ShouldNotBeNull("because the blob should be readable");
        var buffer = new byte[10];
        var bytesRead = contentStream.Read(buffer, 0, 10);
        contentStream.Dispose();
        bytesRead.ShouldBe(0, "because the data is 0 bytes long");
    }

    [Fact]
    public void TestMultipleBlobRead()
    {
        var content = Encoding.UTF8.GetBytes("12345");
        var data = new Blob("text/plain", content);
        var doc = new MutableDocument("doc1");
        doc.SetBlob("data", data);
        data = doc.GetBlob("data");
        data.ShouldNotBeNull("because it was just added");
        for (var i = 0; i < 5; i++) {
            data.Content.ShouldBe(content, "because otherwise incorrect data was read");
            using var contentStream = data.ContentStream;
            var buffer = new byte[10];
            var bytesRead = contentStream?.Read(buffer, 0, 10);
            bytesRead.ShouldBe(5, "because the data has 5 bytes");
        }

        DefaultCollection.Save(doc);

        using var otherDb = new Database(Db.Name, Db.Config);
        
        {
            var doc1 = otherDb.GetDefaultCollection().GetDocument("doc1");
            doc1.ShouldNotBeNull("because it should be accessible from another handle");
            doc1["data"].Value.ShouldBeAssignableTo<Blob>("because otherwise the data did not save correctly");
            data = doc1.GetBlob("data");

            data.ShouldNotBeNull("because otherwise the save didn't persist properly");
            data.Length.ShouldBe(5, "because the data is 5 bytes long");
            data.Content.ShouldBe(content, "because the data should have been retrieved correctly");
            var contentStream = data.ContentStream;
            contentStream.ShouldNotBeNull("because the blob should be readable");
            var buffer = new byte[10];
            var bytesRead = contentStream.Read(buffer, 0, 10);
            contentStream.Dispose();
            bytesRead.ShouldBe(5, "because the data is 5 bytes long");
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
        DefaultCollection.Save(doc);

        ReopenDB();

        var gotDoc = DefaultCollection.GetDocument("doc1");
        gotDoc.ShouldNotBeNull("because otherwise the save failed");
        gotDoc.GetBlob("data")?.Content.ShouldBe(content, "because the data should have been retrieved correctly");

        ReopenDB();

        doc = DefaultCollection.GetDocument("doc1")!.ToMutable();
        doc.SetString("foo", "bar");
        DefaultCollection.Save(doc);
        doc.GetBlob("data")?.Content.ShouldBe(content, "because the data should have been retrieved correctly");
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
        var result = new Dictionary<string, object?>();
        foreach (var item in doc)
        {
            result[item.Key] = item.Value;
        }

        result.ShouldBeEquivalentToFluent(content, "because that is the correct content");
        content = doc.Remove("key2").SetInt("key20", 20).SetInt("key21", 21).ToDictionary();

        result = new Dictionary<string, object?>();
        foreach (var item in doc)
        {
            result[item.Key] = item.Value;
        }

        result.ShouldBeEquivalentToFluent(content, "because that is the correct content");

        SaveDocument(doc, d =>
        {
            result = new Dictionary<string, object?>();
            foreach (var item in d)
            {
                result[item.Key] = item.Value;
            }

            result.ShouldBeEquivalentToFluent(content, "because that is the correct content");
        });
    }

    [Fact]
    public void TestToMutable()
    {
        var content = Encoding.UTF8.GetBytes(BlobStr);
        var data = new Blob("text/plain", content);
        using var mDoc1 = new MutableDocument("doc1");
        mDoc1.SetBlob("data", data);
        mDoc1.SetString("name", "Jim");
        mDoc1.SetInt("score", 10);
        using (var mDoc2 = mDoc1.ToMutable()) {
            mDoc2.ShouldNotBeSameAs(mDoc1);
            mDoc2.GetBlob("data").ShouldBe(mDoc1.GetBlob("data"));
            mDoc2.GetString("name").ShouldBe(mDoc1.GetString("name"));
            mDoc2.GetInt("score").ShouldBe(mDoc1.GetInt("score"));
        }

        SaveDocument(mDoc1);
        using (var doc1 = DefaultCollection.GetDocument(mDoc1.Id)) 
        using (var mDoc3 = doc1?.ToMutable()) {
            mDoc3.ShouldNotBeNull("because otherwise the save failed");
            doc1!.GetBlob("data").ShouldBe(mDoc3.GetBlob("data"));
            doc1.GetString("name").ShouldBe(mDoc3.GetString("name"));
            doc1.GetInt("score").ShouldBe(mDoc3.GetInt("score"));
        }
    }

    [Fact]
    public void TestEquality()
    {
        var data1 = "data1"u8.ToArray();
        var data2 = "data2"u8.ToArray();

        using var doc1A = new MutableDocument("doc1");
        using var doc1B = new MutableDocument("doc1");
        using var doc1C = new MutableDocument("doc1");
        doc1A.SetInt("answer", 42);
        doc1A.SetValue("options", new[] { 1, 2, 3 });
        doc1A.SetBlob("attachment", new Blob("text/plain", data1));

        doc1B.SetInt("answer", 42);
        doc1B.SetValue("options", new[] { 1, 2, 3 });
        doc1B.SetBlob("attachment", new Blob("text/plain", data1));

        doc1C.SetInt("answer", 41);
        doc1C.SetValue("options", new[] { 1, 2 });
        doc1C.SetBlob("attachment", new Blob("text/plain", data2));
        doc1C.SetString("comment", "This is a comment");

        doc1A.ShouldBeEquivalentToFluent(doc1B);
        doc1A.ShouldNotBeEquivalentTo(doc1C);

        doc1B.ShouldBeEquivalentToFluent(doc1A);
        doc1B.ShouldNotBeEquivalentTo(doc1C);

        doc1C.ShouldNotBeEquivalentTo(doc1A);
        doc1C.ShouldNotBeEquivalentTo(doc1B);

        DefaultCollection.Save(doc1C);
        using var savedDoc = DefaultCollection.GetDocument(doc1C.Id);
        using var mDoc = savedDoc?.ToMutable();
        mDoc.ShouldBeEquivalentToFluent(savedDoc);
        mDoc?.SetInt("answer", 50);
        mDoc.ShouldNotBe(savedDoc);
    }

    [Fact]
    public void TestEqualityDifferentDocID()
    {
        using var doc1 = new MutableDocument("doc1");
        using var doc2 = new MutableDocument("doc2");
        doc1.SetInt("answer", 42);
        doc2.SetInt("answer", 42);
        DefaultCollection.Save(doc1);
        DefaultCollection.Save(doc2);
        using var sDoc1 = DefaultCollection.GetDocument(doc1.Id);
        using var sDoc2 = DefaultCollection.GetDocument(doc2.Id);
        sDoc1.ShouldEqual(doc1);
        sDoc2.ShouldEqual(doc2);

        doc1.ShouldEqual(doc1);
        doc1.ShouldNotEqual(doc2);

        doc2.ShouldNotEqual(doc1);
        doc2.ShouldEqual(doc2);

        sDoc1.ShouldNotEqual(sDoc2);
        sDoc2.ShouldNotEqual(sDoc1);
    }

    [Fact]
    public void TestEqualityDifferentDB()
    {
        using var otherDB = OpenDB("other");
        try {
            using (var doc1A = new MutableDocument("doc1"))
            using (var doc1B = new MutableDocument("doc1")) {
                doc1A.SetInt("answer", 42);
                doc1B.SetInt("answer", 42);
                doc1A.ShouldEqual(doc1B);

                DefaultCollection.Save(doc1A);
                otherDB.GetDefaultCollection().Save(doc1B);
                using (var sDoc1A = DefaultCollection.GetDocument(doc1A.Id))
                using (var sDoc1B = otherDB.GetDefaultCollection().GetDocument(doc1B.Id)) {
                    sDoc1A.ShouldEqual(doc1A);
                    sDoc1B.ShouldEqual(doc1B);
                    doc1A.ShouldNotEqual(doc1B);
                    sDoc1A.ShouldNotEqual(sDoc1B);
                }
            }

            using (var sDoc1A = DefaultCollection.GetDocument("doc1"))
            using (var sDoc1B = otherDB.GetDefaultCollection().GetDocument("doc1")) {
                sDoc1A.ShouldNotEqual(sDoc1B);


                using var sameDB = new Database(Db);
                using var anotherDoc1A = sameDB.GetDefaultCollection().GetDocument("doc1");
                sDoc1A.ShouldEqual(anotherDoc1A);
            }
        } finally {
            otherDB.Delete();
        }
    }

    [ForIssue("couchbase-lite-android/1449")]
    [Fact]
    public void TestDeleteDocAndGetDoc()
    {
        const string docID = "doc-1";
        DefaultCollection.GetDocument(docID).ShouldBeNull();
        using (var mDoc = new MutableDocument(docID)) {
            mDoc.SetString("key", "value");
            DefaultCollection.Save(mDoc);
            using (var doc = DefaultCollection.GetDocument(mDoc.Id)) {
                doc.ShouldNotBeNull();
                DefaultCollection.Count.ShouldBe(1UL);
            }

            using (var doc = DefaultCollection.GetDocument(docID)) {
                doc.ShouldNotBeNull();
                doc.GetString("key").ShouldBe("value");
                DefaultCollection.Delete(doc);
                DefaultCollection.Count.ShouldBe(0UL);
            }

            DefaultCollection.GetDocument(docID).ShouldBeNull();
        }

        using (var mDoc = new MutableDocument(docID)) {
            mDoc.SetString("key", "value");
            DefaultCollection.Save(mDoc);
            using (var doc = DefaultCollection.GetDocument(mDoc.Id)) {
                doc.ShouldNotBeNull();
                DefaultCollection.Count.ShouldBe(1UL);
            }

            using (var doc = DefaultCollection.GetDocument(docID)) {
                doc.ShouldNotBeNull();
                doc.GetString("key").ShouldBe("value");
                DefaultCollection.Delete(doc);
                DefaultCollection.Count.ShouldBe(0UL);
            }

            DefaultCollection.GetDocument(docID).ShouldBeNull();
        }
    }

    [Fact]
    public void TestSetAndGetExpirationFromDoc()
    {
        var dto30 = DateTimeOffset.UtcNow.AddSeconds(30);

        using (var doc1A = new MutableDocument("doc1"))
        using (var doc1B = new MutableDocument("doc2"))
        using (var doc1C = new MutableDocument("doc3")) {
            doc1A.SetInt("answer", 12);
            doc1A.SetValue("options", new[] { 1, 2, 3 });
            DefaultCollection.Save(doc1A);

            doc1B.SetInt("answer", 22);
            doc1B.SetValue("options", new[] { 1, 2, 3 });
            DefaultCollection.Save(doc1B);

            doc1C.SetInt("answer", 32);
            doc1C.SetValue("options", new[] { 1, 2, 3 });
            DefaultCollection.Save(doc1C);

            DefaultCollection.SetDocumentExpiration("doc1", dto30).ShouldBe(true);
            DefaultCollection.SetDocumentExpiration("doc3", dto30).ShouldBe(true);
        }
        DefaultCollection.SetDocumentExpiration("doc3", null).ShouldBe(true);
        var v = DefaultCollection.GetDocumentExpiration("doc1");
        v.ShouldBeEquivalentToFluent(dto30.DateTime);
        DefaultCollection.GetDocumentExpiration("doc2").ShouldBe(null);
        DefaultCollection.GetDocumentExpiration("doc3").ShouldBe(null);
    }

#if !SANITY_ONLY
        [Fact]
        public void TestSetExpirationOnDoc()
        {
            var dto3 = DateTimeOffset.UtcNow.AddSeconds(3);
            using (var doc1A = new MutableDocument("doc_to_expired")) {
                doc1A.SetInt("answer", 12);
                doc1A.SetValue("options", (int[])[1, 2, 3]);
                DefaultCollection.Save(doc1A);

                DefaultCollection.SetDocumentExpiration("doc_to_expired", dto3).ShouldBe(true);

            }
            Thread.Sleep(3100);
            Try.Condition(() => DefaultCollection.GetDocument("doc_to_expired") == null)
                .Times(5)
                .WriteProgress(WriteLine)
                .Delay(TimeSpan.FromMilliseconds(500))
                .Go().ShouldBeTrue();
        }

        [Fact]
        public void TestSetExpirationOnDeletedDoc()
        {
            var dto3 = DateTimeOffset.Now.AddSeconds(3);
            using var doc1A = new MutableDocument("deleted_doc1");
            doc1A.SetInt("answer", 12);
            doc1A.SetValue("options", (int[])[1, 2, 3]);
            DefaultCollection.Save(doc1A);
            DefaultCollection.Delete(doc1A);

            DefaultCollection.SetDocumentExpiration("deleted_doc1", dto3).ShouldBeTrue();
            Thread.Sleep(3100);

            Action badAction = (() => DefaultCollection.SetDocumentExpiration("deleted_doc1", dto3));
            Try.Assertion(() => Should.Throw<CouchbaseLiteException>(badAction, "because the document has been purged"))
                .Times(5).WriteProgress(WriteLine).Delay(TimeSpan.FromMilliseconds(500)).Go().ShouldBeTrue();
        }
#endif

    [Fact]
    public void TestGetExpirationFromDeletedDoc()
    {
        var dto3 = DateTimeOffset.UtcNow.AddSeconds(3);
        using (var doc1A = new MutableDocument("deleted_doc")) {
            doc1A.SetInt("answer", 12);
            doc1A.SetValue("options", new[] { 1, 2, 3 });
            DefaultCollection.Save(doc1A);
            DefaultCollection.SetDocumentExpiration("deleted_doc", dto3).ShouldBe(true);
            DefaultCollection.Delete(doc1A);  
        }
        var exp = DefaultCollection.GetDocumentExpiration("deleted_doc");
        exp.ShouldBeEquivalentToFluent(dto3);
    }

    [Fact]
    public void TestSetExpirationOnNoneExistDoc()
    {
        var dto30 = DateTimeOffset.Now.AddSeconds(30);
        Should.Throw<CouchbaseLiteException>(() => DefaultCollection.SetDocumentExpiration("not_exist", dto30), "because the document does not exist");
    }
        
    [Fact]
    public void TestGetExpirationFromNoneExistDoc()
    {
        Should.Throw<CouchbaseLiteException>(() => DefaultCollection.GetDocumentExpiration("not_exist"), "because the document does not exist");
    }

    [Fact]
    public void TestLongExpiration()
    {
        var now = DateTime.UtcNow;
        using var doc = new MutableDocument("doc");
        doc.SetInt("answer", 42);
        doc.SetValue("options", (int[])[1, 2, 3]);
        DefaultCollection.Save(doc);

        DefaultCollection.GetDocumentExpiration("doc").ShouldBeNull();
        DefaultCollection.SetDocumentExpiration("doc", DateTimeOffset.UtcNow.AddDays(60));

        var exp = DefaultCollection.GetDocumentExpiration("doc");
        exp.ShouldNotBeNull();
        (Math.Abs((exp.Value - now).TotalDays - 60.0) < 1.0).ShouldBeTrue();
    }

#if !SANITY_ONLY
        [Fact]
        public void TestSetAndUnsetExpirationOnDoc()
        {
            var dto3 = DateTimeOffset.UtcNow.AddSeconds(3);
            using (var doc1A = new MutableDocument("doc_to_expired")) {
                doc1A.SetInt("answer", 12);
                doc1A.SetValue("options", (int[])[1, 2, 3]);
                DefaultCollection.Save(doc1A);

                DefaultCollection.SetDocumentExpiration("doc_to_expired", dto3).ShouldBe(true);

            }
            DefaultCollection.SetDocumentExpiration("doc_to_expired", null).ShouldBe(true);

            Thread.Sleep(3100);
            DefaultCollection.GetDocument("doc_to_expired").ShouldNotBeNull();
        }

        [Fact]
        public void TestDocumentExpirationAfterDocsExpired()
        {
            var dto2 = DateTimeOffset.Now.AddSeconds(2);
            var dto3 = DateTimeOffset.Now.AddSeconds(3);
            var dto4 = DateTimeOffset.Now.AddSeconds(4);

            using (var doc1A = new MutableDocument("doc1"))
            using (var doc1B = new MutableDocument("doc2"))
            using (var doc1C = new MutableDocument("doc3")) {
                doc1A.SetInt("answer", 42);
                doc1A.SetString("a", "string");
                DefaultCollection.Save(doc1A);

                doc1B.SetInt("answer", 42);
                doc1B.SetString("b", "string");
                DefaultCollection.Save(doc1B);

                doc1C.SetInt("answer", 42);
                doc1C.SetString("c", "string");
                DefaultCollection.Save(doc1C);

                DefaultCollection.SetDocumentExpiration("doc1", dto2).ShouldBe(true);
                DefaultCollection.SetDocumentExpiration("doc2", dto3).ShouldBe(true);
                DefaultCollection.SetDocumentExpiration("doc3", dto4).ShouldBe(true);
            }

            Thread.Sleep(4100);

            Try.Assertion(() =>
            {
                DefaultCollection.GetDocument("doc1").ShouldBeNull();
                DefaultCollection.GetDocument("doc2").ShouldBeNull();
                DefaultCollection.GetDocument("doc3").ShouldBeNull();
            }).Times(5).WriteProgress(WriteLine).Delay(TimeSpan.FromMilliseconds(500)).Go().ShouldBeTrue();
        }
#endif

    [Fact]
    public void TestExpireNow()
    {
        const string docId = "byebye";
        using (var doc1 = new MutableDocument(docId)) {
            doc1.SetString("expire_me", "now");
            DefaultCollection.Save(doc1);
        }

        DefaultCollection.GetDocument(docId).ShouldNotBeNull("because the expiration has not been set yet");
        DefaultCollection.SetDocumentExpiration(docId, DateTimeOffset.Now);
        Thread.Sleep(50);
        DefaultCollection.GetDocument(docId).ShouldBeNull("because the purge should happen immediately");
    }

#if !SANITY_ONLY
        [Fact]
        public void TestPurgeEvent()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("directory", "garbage");
                DefaultCollection.Save(doc1);
            }
            
            using var mre = new ManualResetEventSlim();
            DefaultCollection.AddChangeListener((_, _) => mre.Set());

            DefaultCollection.Purge("doc1");
            Thread.Sleep(1000);
            mre.Wait(TimeSpan.FromSeconds(1)).ShouldBeTrue("because purge should fire a changed event");
        }
#endif

    [Fact]
    public void TestRevisionIDNewDoc()
    {
        using var doc = new MutableDocument();
        doc.RevisionID.ShouldBeNull();
        DefaultCollection.Save(doc);
        doc.RevisionID.ShouldNotBeNull();
    }

    [Fact]
    public void TestRevisionIDExistingDoc()
    {
        using (var doc = new MutableDocument("doc1")) {
            DefaultCollection.Save(doc);
        }

        using (var doc = DefaultCollection.GetDocument("doc1"))
        using (var mutableDoc = doc?.ToMutable()) {
            mutableDoc.ShouldNotBeNull("because otherwise the save failed");
            var docRevId = doc?.RevisionID;
            doc!.RevisionID.ShouldBe(mutableDoc.RevisionID);
            mutableDoc.SetInt("int", 88);
            DefaultCollection.Save(mutableDoc);
            doc.RevisionID.ShouldNotBe(mutableDoc.RevisionID);
            docRevId.ShouldBe(doc.RevisionID);
        }
    }

    [Fact]
    public void TestTypesInDocumentToJSON()
    {
        var dic = PopulateDictData();
        using (var md = new MutableDocument("doc1")) {
            foreach (var item in dic) {
                md.SetValue(item.Key, item.Value);
            }

            DefaultCollection.Save(md);
        }

        using (var doc = DefaultCollection.GetDocument("doc1")) {
            var json = doc?.ToJSON();
            json.ShouldNotBeNull("because otherwise converting to JSON failed");
            ValidateToJsonValues(json, dic);
        }
    }

    [Fact]
    public void TestMutableDocWithJsonString()
    {
        var dic = PopulateDictData();
        var dicJson = JsonSerializer.Serialize(dic);
        using var md = new MutableDocument("doc1", dicJson);
        ValidateValuesInMutableDictFromJson(dic, md);
    }

    [Fact]
    public void TestMutableDocToJsonThrowException()
    {
        var md = new MutableDocument();
        Should.Throw<NotSupportedException>(md.ToJSON);
    }

    [Fact]
    public void TestMutableDocumentSetJsonWithInvalidParam()
    {
        using var md = new MutableDocument("doc1");
        // with random string 
        Should.Throw<CouchbaseLiteException>(() => md.SetJSON("random string"));

        //with array json string    
        string[] arr = ["apple", "banana", "orange"];
        var jArr = JsonSerializer.Serialize(arr);
        Should.Throw<CouchbaseLiteException>(() => md.SetJSON(jArr));
    }

    [Fact]
    public void TestCreateMutableDocWithInvalidStr()
    {
        // with random string 
        Should.Throw<CouchbaseLiteException>(() => new MutableDocument("doc1", "random string"));

        //with array json string    
        string[] arr = ["apple", "banana", "orange"];
        var jArr = JsonSerializer.Serialize(arr);
        Should.Throw<CouchbaseLiteException>(() => new MutableDocument("doc1", jArr));
    }

    [Fact]
    public void TestSaveFromDifferentDBInstance()
    {
        using var otherHandle = new Database(Db.Name, Db.Config);
        using var collection1Db1 = Db.CreateCollection("cats", "mammals");
        using var bengalCreate = new MutableDocument("bengal");
        bengalCreate.SetString("type", "bengal");
        collection1Db1.Save(bengalCreate);

        using var differentDbCollection = otherHandle.GetCollection("cats", "mammals");
        differentDbCollection.ShouldNotBeNull("because it should be accessible from another handle");

        bengalCreate.ShouldNotBeNull("because otherwise it vanished from the collection");
        bengalCreate.SetInt("age", 10);
        Should.Throw<CouchbaseLiteException>(() => differentDbCollection.Save(bengalCreate),
            "because the collection is from a different database object");
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
        doc.SetBlob("blob", ArrayTestBlob());
    }
}
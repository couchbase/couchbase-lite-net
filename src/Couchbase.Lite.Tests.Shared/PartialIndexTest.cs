//
//  PartialIndexTest.cs
//
//  Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite;
using Couchbase.Lite.Query;
using Shouldly;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test;

[ImplementsTestSpec("T0007-Partial-Index", "1.0.3")]
public class PartialIndexTest(ITestOutputHelper output) : TestCase(output)
{
    /// <summary>
    /// Test that a partial value index is successfully created.
    ///     
    /// Steps
    /// 1. Create a partial value index named "numIndex" in the default collection.
    ///     - expression: "num"
    ///     - where: "type = 'number'"
    /// 2. Check that the index is successfully created.
    /// 3. Create a query object with an SQL++ string:
    ///     - SELECT * FROM _ WHERE type = 'number' AND num > 1000
    /// 4. Get the query plan from the query object and check that the plan contains
    ///     "USING INDEX numIndex" string.
    /// 5. Create a query object with an SQL++ string:
    ///     - SELECT * FROM _ WHERE type = 'foo' AND num > 1000
    /// 6. Get the query plan from the query object and check that the plan doesn't contain
    ///     "USING INDEX numIndex" string.
    /// </summary>
    [Fact]
    public void TestCreatePartialValueIndex()
    {
        // Step 1
        var indexConfig = new ValueIndexConfiguration(["num"], "type = 'number'");
        DefaultCollection.CreateIndex("numIndex", indexConfig);

        // Step 2
        DefaultCollection.GetIndexes().ShouldContain("numIndex", "because the index was just created");

        // Step 3
        using var partialQuery = Db.CreateQuery("SELECT * FROM _ WHERE type = 'number' AND num > 1000");

        // Step 4
        partialQuery.Explain().Contains("USING INDEX numIndex")
            .ShouldBeTrue("because the partial index should be applied to this query");

        // Step 5
        using var nonPartialQuery = Db.CreateQuery("SELECT * FROM _ WHERE type = 'foo' AND num > 1000");

        // Step 6
        nonPartialQuery.Explain().Contains("USING INDEX numIndex")
            .ShouldBeFalse("because the partial index should not be applied to this query");
    }

    /// <summary>
    /// Test that a partial full text index is successfully created.
    ///
    /// Steps
    /// 1. Create following two documents with the following bodies in the default collection.
    ///     - { "content" : "Couchbase Lite is a database." }
    ///     - { "content" : "Couchbase Lite is a NoSQL syncable database." }
    /// 2. Create a partial full text index named "contentIndex" in the default collection.
    ///     - expression: "content"
    ///     - where: "length(content) > 30"
    /// 3. Check that the index is successfully created.
    /// 4. Create a query object with an SQL++ string:
    ///     - SELECT content FROM _ WHERE match(contentIndex, "database")
    /// 5. Execute the query and check that:
    ///     - There is one result returned
    ///     - The returned content is "Couchbase Lite is a NoSQL syncable database."
    /// </summary>
    [Fact]
    public void TestCreatePartialFullTextIndex()
    {
        // Step 1
        using var doc1 = new MutableDocument();
        using var doc2 = new MutableDocument();
        doc1.SetString("content", "Couchbase Lite is a database.");
        doc2.SetString("content", "Couchbase Lite is a NoSQL syncable database.");
        DefaultCollection.Save(doc1);
        DefaultCollection.Save(doc2);

        // Step 2
        var indexConfig = new FullTextIndexConfiguration(["content"], "length(content) > 30");
        DefaultCollection.CreateIndex("contentIndex", indexConfig);

        // Step 3
        DefaultCollection.GetIndexes().ShouldContain("contentIndex", "because the index was just created");

        // Step 4
        using var query = Db.CreateQuery("SELECT content FROM _ WHERE match(contentIndex, 'database')");

        // Step 5
        var results = query.Execute().ToList();
        results.Count.ShouldBe(1, "because only one document matches the partial index criteria");
        results[0].GetString("content").ShouldBe("Couchbase Lite is a NoSQL syncable database.", "because this is the document that matches the query");
    }
}

// 
// URLEndpointListenerTest.cs
// 
// Copyright (c) 2024 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 

using Couchbase.Lite;
using Shouldly;

using Xunit;
using Xunit.Abstractions;
using Couchbase.Lite.Unsupported;
using Couchbase.Lite.Sync;
using System.Threading;

namespace Test;

public sealed class VersionVectorTest(ITestOutputHelper output) : TestCase(output)
{
    /// <summary>
    /// Description
    ///     Test that the document's timestamp returns value as expected.
    ///
    /// Steps
    ///     1. Create a new document with id = "doc1"
    ///     2. Get document's timestamp and check that the timestamp is 0.
    ///     3. Save the document into the default collection.
    ///     4. Get document's timestamp and check that the timestamp is more than 0.
    ///     5. Get the document id = "doc1" from the database.
    ///     6. Get document's timestamp and check that the timestamp is the same as the timestamp from step 4.
    /// </summary>
    [Fact]
    public void TestDocumentTimestamp()
    {
        using var doc = new MutableDocument("doc1");
        doc.Timestamp.ShouldBe(0UL, "because the doc has not been saved yet");
        DefaultCollection.Save(doc);
        doc.Timestamp.ShouldNotBe(0UL, "because the doc is now saved");
        using var savedDoc = DefaultCollection.GetDocument("doc1");
        savedDoc.ShouldNotBeNull("because the document was just saved");
        savedDoc.Timestamp.ShouldBe(doc.Timestamp, "because the timestamp should not change just from a read");
    }

    /// <summary>
    /// Description
    ///     Test that the document's timestamp returns value as expected.
    ///     
    /// Steps
    ///     1. Create a new document with id = "doc1"
    ///     2. Get document's _revisionIDs and check that the value returned is null.
    ///     3. Save the document into the default collection.
    ///     4. Get document's _revisionIDs and check that the value returned is not null
    ///     5. Get the document id = "doc1" from the database.
    ///     6. Get document's _revisionIDs and check that the value returned is not null
    /// </summary>
    [Fact]
    public void TestDocumentRevisionHistory()
    {
        using var doc = new MutableDocument("doc1");
        doc.RevisionIDs().ShouldBeNull("because the document has not been saved yet");
        DefaultCollection.Save(doc);
        doc.RevisionIDs().ShouldNotBeNull("because now the document has been saved");
        using var savedDoc = DefaultCollection.GetDocument("doc1");
        savedDoc.ShouldNotBeNull("because the document was just saved");

        savedDoc.RevisionIDs().ShouldNotBeNull("because the saved document should contain at least one revision ID");
    }

    public enum ConflictLastWriteWinsMode
    {
        SaveDB2First,
        SaveDB1First
    }

    /// <summary>
    /// Description
    ///     Test that the default conflict resolver that the last write wins works as expected.
    ///     There could be already a default conflict resolver test that can be modified to test this
    ///     test case.
    ///
    /// Steps
    ///     1. Create two databases the names such as "db1" and "db2".
    ///     2. Create a document on each database in the exact order as :
    ///         - Document id "doc1" on "db2" with content as {"key": "value2"}
    ///         - Document id "doc1" on "db1" with content as {"key": "value1"} 
    ///     3. Start a single shot pull replicator to pull documents from "db2" to "db1".
    ///     4. Get the document "doc1" from "db1" and check that the content is {"key": "value1"}.
    ///     5.Create a document on each database in the exact order as :
    ///         -Document id "doc2" on "db1" with content as {"key": "value1"} 
    ///         -Document id "doc2" on "db2" with content as {"key": "value2"}
    ///     6.Start a single shot pull replicator to pull documents from "db2" to "db1".
    ///     7. Get the document "doc2" from "db1" and check that the content is {"key": "value2"}.
    /// </summary>
    [Theory]
    [InlineData(ConflictLastWriteWinsMode.SaveDB2First)]
    [InlineData(ConflictLastWriteWinsMode.SaveDB1First)]
    public void TestDefaultConflictResolver(ConflictLastWriteWinsMode lastWriteWinsMode)
    {
        Database.Delete("db1", null);
        Database.Delete("db2", null);

        using var db1 = new Database("db1");
        using var db2 = new Database("db2");

        using var db2Doc = new MutableDocument("doc1");
        db2Doc["key"].Value = "value2";
        using var db1Doc = new MutableDocument("doc1");
        db1Doc["key"].Value = "value1";

        string expectedValue;
        if (lastWriteWinsMode == ConflictLastWriteWinsMode.SaveDB2First) {
            db2.GetDefaultCollection().Save(db2Doc);
            db1.GetDefaultCollection().Save(db1Doc);
            expectedValue = "value1";
        } else {
            db1.GetDefaultCollection().Save(db1Doc);
            db2.GetDefaultCollection().Save(db2Doc);
            expectedValue = "value2";
        }
        
        var replConfig = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(db1.GetDefaultCollection()),
            new DatabaseEndpoint(db2))
        {
            ReplicatorType = ReplicatorType.Pull
        };
        
        using var repl = new Replicator(replConfig);
        repl.Start();
        while (repl.Status.Activity != ReplicatorActivityLevel.Stopped) {
            Thread.Sleep(100);
        }

        using var doc = db1.GetDefaultCollection().GetDocument("doc1");
        doc.ShouldNotBeNull("because it was just saved");
        doc["key"].Value.ShouldBe(expectedValue, "because otherwise the conflict resolver behaved unexpectedly");
    }

    /// <summary>
    /// Description
    ///     Test that the default conflict resolver that the delete operation always wins works as expected.
    ///     There could be already a default conflict resolver test that can be modified to test this
    ///     test case.
    ///
    /// Steps
    ///     1. Create two databases the names such as "db1" and "db2".
    ///     2. Create a document on each database as :
    ///         - Document id "doc1" on "db1" with content as {"key": "value1"} 
    ///         - Document id "doc1" on "db2" with content as {"key": "value2"}
    ///     3. Update the document on each database as the following order:
    ///         - Delete document id "doc1" on "db1"
    ///         - Update document id "doc1" on "db2" as {"key": "value3"}
    ///     4.Start a single shot pull replicator to pull documents from "db2" to "db1".
    ///     5. Get the document "doc1" from "db1" and check that the returned document is null.
    /// </summary>
    [Fact]
    public void TestDefaultConflictResolverDeleteWins()
    {
        Database.Delete("db1", null);
        Database.Delete("db2", null);

        using var db1 = new Database("db1");
        using var db2 = new Database("db2");

        using var db2Doc = new MutableDocument("doc1");
        db2Doc["key"].Value = "value2";
        using var db1Doc = new MutableDocument("doc1");
        db1Doc["key"].Value = "value1";

        db1.GetDefaultCollection().Save(db1Doc);
        db2.GetDefaultCollection().Save(db2Doc);
        db1.GetDefaultCollection().Delete(db1Doc);

        db2Doc["key"].Value = "value3";
        db2.GetDefaultCollection().Save(db2Doc);

        var replConfig = new ReplicatorConfiguration(CollectionConfiguration.FromCollections(db1.GetDefaultCollection()),
            new DatabaseEndpoint(db2))
        {
            ReplicatorType = ReplicatorType.Pull
        };
        
        using var repl = new Replicator(replConfig);
        repl.Start();
        while (repl.Status.Activity != ReplicatorActivityLevel.Stopped) {
            Thread.Sleep(100);
        }

        using var finalDoc = db1.GetDefaultCollection().GetDocument("doc1");
        finalDoc.ShouldBeNull("because the deletion should win the conflict");
    }
}
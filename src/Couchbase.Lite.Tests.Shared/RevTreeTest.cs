//
// RevTreeTest.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
/*
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
* except in compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the
* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
* either express or implied. See the License for the specific language governing permissions
* and limitations under the License.
*/

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;
using System;

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class RevTreeTest : LiteTestCase
    {
        public const string Tag = "RevTree";

        public RevTreeTest(string storageType) : base(storageType) {}

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestForceInsertEmptyHistory()
        {
            var rev = new RevisionInternal("FakeDocId", "1-abcd", false);
            var revProperties = new Dictionary<string, object>();
            revProperties.Put("_id", rev.DocID);
            revProperties.Put("_rev", rev.RevID);
            revProperties["message"] = "hi";
            rev.SetProperties(revProperties);

            IList<string> revHistory = null;
            database.ForceInsert(rev, revHistory, null);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestRevTree()
        {
            var rev = new RevisionInternal("MyDocId", "4-abcd", false);
            var revProperties = new Dictionary<string, object>();
            revProperties.Put("_id", rev.DocID);
            revProperties.Put("_rev", rev.RevID);
            revProperties["message"] = "hi";
            rev.SetProperties(revProperties);

            var revHistory = new List<string>();
            revHistory.Add(rev.RevID);
            revHistory.Add("3-abcd");
            revHistory.Add("2-abcd");
            revHistory.Add("1-abcd");
            database.ForceInsert(rev, revHistory, null);
            Assert.AreEqual(1, database.GetDocumentCount());

            VerifyHistory(database, rev, revHistory);
            var conflict = new RevisionInternal("MyDocId", "5-abcd", false);
            var conflictProperties = new Dictionary<string, object>();
            conflictProperties.Put("_id", conflict.DocID);
            conflictProperties.Put("_rev", conflict.RevID);
            conflictProperties["message"] = "yo";
            conflict.SetProperties(conflictProperties);
            
            var conflictHistory = new List<string>();
            conflictHistory.Add(conflict.RevID);
            conflictHistory.Add("4-bcde");
            conflictHistory.Add("3-bcde");
            conflictHistory.Add("2-abcd");
            conflictHistory.Add("1-abcd");
            database.ForceInsert(conflict, conflictHistory, null);
            Assert.AreEqual(1, database.GetDocumentCount());
            VerifyHistory(database, conflict, conflictHistory);
            
            // Add an unrelated document:
            var other = new RevisionInternal("AnotherDocID", "1-cdef", false);
            var otherProperties = new Dictionary<string, object>();
            otherProperties["language"] = "jp";
            other.SetProperties(otherProperties);
            var otherHistory = new List<string>();
            otherHistory.Add(other.RevID);
            database.ForceInsert(other, otherHistory, null);
            
            // Fetch one of those phantom revisions with no body:
            var rev2 = database.GetDocument(rev.DocID, "2-abcd", 
                true);
            Assert.IsNull(rev2);

            // Make sure no duplicate rows were inserted for the common revisions:
            Assert.IsTrue(database.GetLastSequenceNumber() <= 8);
            // Make sure the revision with the higher revID wins the conflict:
            var current = database.GetDocument(rev.DocID, null, 
                true);
            Assert.AreEqual(conflict, current);
            
            // Get the _changes feed and verify only the winner is in it:
            var options = new ChangesOptions();
            var changes = database.ChangesSince(0, options, null, null);
            var expectedChanges = new RevisionList();
            expectedChanges.Add(conflict);
            expectedChanges.Add(other);
            Assert.AreEqual(expectedChanges, changes);
            options.IncludeConflicts = true;
            changes = database.ChangesSince(0, options, null, null);
            expectedChanges = new RevisionList();
            expectedChanges.Add(rev);
            expectedChanges.Add(conflict);
            expectedChanges.Add(other);
            var expectedChangesAlt = new RevisionList();
            expectedChangesAlt.Add(conflict);
            expectedChangesAlt.Add(rev);
            expectedChangesAlt.Add(other);
            Assert.IsTrue(expectedChanges.SequenceEqual(changes) || expectedChangesAlt.SequenceEqual(changes));
        }

        [Test]
        public void TestRevTreeChangeNotification()
        {
            const string DOCUMENT_ID = "MyDocId";

            var rev = new RevisionInternal(DOCUMENT_ID, "1-abcd", false);
            var revProperties = new Dictionary<string, object>();
            revProperties["_id"] = rev.DocID;
            revProperties["_rev"] = rev.RevID;
            revProperties["message"] = "hi";
            rev.SetProperties(revProperties);

            var revHistory = new List<string>();
            revHistory.Add(rev.RevID);

            EventHandler<DatabaseChangeEventArgs> handler = (sender, e) =>
            {
                var changes = e.Changes.ToList();
                Assert.AreEqual(1, changes.Count);
                var change = changes[0];
                Assert.AreEqual(DOCUMENT_ID, change.DocumentId);
                Assert.AreEqual(rev.RevID, change.RevisionId);
                Assert.IsTrue(change.IsCurrentRevision);
                Assert.IsFalse(change.IsConflict);

                var current = database.GetDocument(change.DocumentId).CurrentRevision;
                Assert.AreEqual(rev.RevID, current.Id);
            };

            database.Changed += handler;
            database.ForceInsert(rev, revHistory, null);
            database.Changed -= handler;

            // add two more revisions to the document
            var rev3 = new RevisionInternal(DOCUMENT_ID, "3-abcd", false);
            var rev3Properties = new Dictionary<string, object>();
            rev3Properties["_id"] = rev3.DocID;
            rev3Properties["_rev"] = rev3.RevID;
            rev3Properties["message"] = "hi again";
            rev3.SetProperties(rev3Properties);

            var rev3History = new List<string>();
            rev3History.Add(rev3.RevID);
            rev3History.Add("2-abcd");
            rev3History.Add(rev.RevID);

            handler = (sender, e) =>
            {
                var changes = e.Changes.ToList();
                Assert.AreEqual(1, changes.Count);
                var change = changes[0];
                Assert.AreEqual(DOCUMENT_ID, change.DocumentId);
                Assert.AreEqual(rev3.RevID, change.RevisionId);
                Assert.IsTrue(change.IsCurrentRevision);
                Assert.IsFalse(change.IsConflict);

                var doc = database.GetDocument(change.DocumentId);
                Assert.AreEqual(rev3.RevID, doc.CurrentRevisionId);
                try
                {
                    Assert.AreEqual(3, doc.RevisionHistory.ToList().Count);
                }
                catch (CouchbaseLiteException)
                {
                    Assert.Fail();
                }
            };

            database.Changed += handler;
            database.ForceInsert(rev3, rev3History, null);
            database.Changed -= handler;

            // add a conflicting revision, with the same history length as the last revision we
            // inserted. Since this new revision's revID has a higher ASCII sort, it should become the
            // new winning revision.
            var conflictRev = new RevisionInternal(DOCUMENT_ID, "3-bcde", false);
            var conflictProperties = new Dictionary<string, object>();
            conflictProperties["_id"] = conflictRev.DocID;
            conflictProperties["_rev"] = conflictRev.RevID;
            conflictProperties["message"] = "winner";
            conflictRev.SetProperties(conflictProperties);

            var conflictRevHistory = new List<string>();
            conflictRevHistory.Add(conflictRev.RevID);
            conflictRevHistory.Add("2-abcd");
            conflictRevHistory.Add(rev.RevID);

            handler = (sender, e) =>
            {
                var changes = e.Changes.ToList();
                Assert.AreEqual(1, changes.Count);
                var change = changes[0];
                Assert.AreEqual(DOCUMENT_ID, change.DocumentId);
                Assert.AreEqual(conflictRev.RevID, change.RevisionId);
                Assert.IsTrue(change.IsCurrentRevision);
                Assert.IsFalse(change.IsConflict);

                var doc = database.GetDocument(change.DocumentId);
                Assert.AreEqual(rev3.RevID, doc.CurrentRevisionId);
                try
                {
                    Assert.AreEqual(2, doc.ConflictingRevisions.ToList().Count);
                    Assert.AreEqual(3, doc.RevisionHistory.ToList().Count);
                }
                catch (CouchbaseLiteException)
                {
                    Assert.Fail();
                }
            };

            database.Changed += handler;
            database.ForceInsert(conflictRev, conflictRevHistory, null);
            database.Changed -= handler;
        }

        private void VerifyHistory(Database db, RevisionInternal rev, IList<string> history)
        {
            var gotRev = db.GetDocument(rev.DocID, null, 
                true);
            Assert.AreEqual(rev, gotRev);
            AssertPropertiesAreEqual(rev.GetProperties(), gotRev.GetProperties());

            var revHistory = db.Storage.GetRevisionHistory(gotRev, null);
            Assert.AreEqual(history.Count, revHistory.Count);
            
            for (int i = 0; i < history.Count; i++)
            {
                RevisionInternal hrev = revHistory[i];
                Assert.AreEqual(rev.DocID, hrev.DocID);
                Assert.AreEqual(history[i], hrev.RevID);
                Assert.IsFalse(rev.Deleted);
            }
        }
    }
}

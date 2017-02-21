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

using System;
using System.Collections.Generic;
using System.Linq;

using Couchbase.Lite.Internal;
using NUnit.Framework;
using Couchbase.Lite.Revisions;
using System.Threading;

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
            var rev = new RevisionInternal("FakeDocId", "1-abcd".AsRevID(), false);
            var revProperties = new Dictionary<string, object>();
            revProperties.SetDocRevID(rev.DocID, rev.RevID);
            revProperties["message"] = "hi";
            rev.SetProperties(revProperties);

            IList<RevisionID> revHistory = null;
            database.ForceInsert(rev, revHistory, null);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestRevTree()
        {
            var are = new AutoResetEvent (false);
            var change = default(DocumentChange);
            database.Changed += (sender, args) =>
            {
                Assert.AreEqual(1, args.Changes.Count());
                Assert.IsNull(change, "Multiple notifications posted");
                change = args.Changes.First();
                are.Set ();
            };

            var rev = new RevisionInternal("MyDocId", "4-4444".AsRevID(), false);
            var revProperties = new Dictionary<string, object>();
            revProperties.SetDocRevID(rev.DocID, rev.RevID);
            revProperties["message"] = "hi";
            rev.SetProperties(revProperties);

            var revHistory = new List<RevisionID>();
            revHistory.Add(rev.RevID);
            revHistory.Add("3-3333".AsRevID());
            revHistory.Add("2-2222".AsRevID());
            revHistory.Add("1-1111".AsRevID());
            database.ForceInsert(rev, revHistory, null);
            Assert.AreEqual(1, database.GetDocumentCount());
            VerifyRev(rev, revHistory);
            Assert.IsTrue (are.WaitOne (5000));
            Assert.AreEqual(Announcement(database, rev, rev), change);
            Assert.IsFalse(change.IsConflict);

            // No-op ForceInsert of already-existing revision
            var lastSeq = database.GetLastSequenceNumber();
            database.ForceInsert(rev, revHistory, null);
            Assert.AreEqual(lastSeq, database.GetLastSequenceNumber());
            
            var conflict = new RevisionInternal("MyDocId", "5-5555".AsRevID(), false);
            var conflictProperties = new Dictionary<string, object>();
            conflictProperties.SetDocRevID(conflict.DocID, conflict.RevID);
            conflictProperties["message"] = "yo";
            conflict.SetProperties(conflictProperties);
            
            var conflictHistory = new List<RevisionID>();
            conflictHistory.Add(conflict.RevID);
            conflictHistory.Add("4-4545".AsRevID());
            conflictHistory.Add("3-3030".AsRevID());
            conflictHistory.Add("2-2222".AsRevID());
            conflictHistory.Add("1-1111".AsRevID());
            change = null;
            database.ForceInsert(conflict, conflictHistory, null);
            Assert.AreEqual(1, database.GetDocumentCount());
            VerifyRev(conflict, conflictHistory);
            Assert.IsTrue (are.WaitOne (5000));
            Assert.AreEqual(Announcement(database, conflict, conflict), change);
            Assert.IsTrue(change.IsConflict);

            // Add an unrelated document:
            var other = new RevisionInternal("AnotherDocID", "1-1010".AsRevID(), false);
            var otherProperties = new Dictionary<string, object>();
            otherProperties["language"] = "jp";
            other.SetProperties(otherProperties);
            var otherHistory = new List<RevisionID>();
            otherHistory.Add(other.RevID);
            change = null;
            database.ForceInsert(other, otherHistory, null);
            Assert.IsTrue (are.WaitOne (5000));
            Assert.AreEqual(Announcement(database, other, other), change);
            Assert.IsFalse(change.IsConflict);

            // Fetch one of those phantom revisions with no body:
            var rev2 = database.GetDocument(rev.DocID, "2-2222".AsRevID(), 
                true);
            Assert.IsTrue(rev2.Missing);
            Assert.IsNull(rev2.GetBody());

            Assert.IsNull(database.GetDocument(rev.DocID, "666-6666".AsRevID(), true));

            // Make sure no duplicate rows were inserted for the common revisions:
            if(_storageType == StorageEngineTypes.SQLite) {
                Assert.AreEqual(8, database.GetLastSequenceNumber());
            } else {
                Assert.AreEqual(3, database.GetLastSequenceNumber());
            }
            // Make sure the revision with the higher revID wins the conflict:
            var current = database.GetDocument(rev.DocID, null, 
                true);
            Assert.AreEqual(conflict, current);

            // Check that the list of conflicts is accurate
            var conflictingRevs = database.Storage.GetAllDocumentRevisions(rev.DocID, true, false);
            CollectionAssert.AreEqual(new[] { conflict, rev }, conflictingRevs);
            
            // Get the _changes feed and verify only the winner is in it:
            var options = ChangesOptions.Default;
            IList<RevisionInternal> changes = database.ChangesSince(0, options, null, null);
            CollectionAssert.AreEqual(new[] { conflict, other }, changes);
            changes = database.ChangesSinceStreaming(0, options, null, null).ToList();
            CollectionAssert.AreEqual(new[] { conflict, other }, changes);
            options.IncludeConflicts = true;
            changes = database.ChangesSince(0, options, null, null);
            var expectedChanges = new RevisionList();
            expectedChanges.Add(rev);
            expectedChanges.Add(conflict);
            expectedChanges.Add(other);
            var expectedChangesAlt = new RevisionList();
            expectedChangesAlt.Add(conflict);
            expectedChangesAlt.Add(rev);
            expectedChangesAlt.Add(other);
            Assert.IsFalse(changes.Except(expectedChanges).Any());
            changes = database.ChangesSinceStreaming(0, options, null, null).ToList();
            Assert.IsFalse(changes.Except(expectedChanges).Any());

            conflict = new RevisionInternal(conflict.DocID, "6-6666".AsRevID(), false);
            conflictHistory.Add(conflict.RevID);
            database.ForceInsert(conflict, conflictHistory, null);

            options = ChangesOptions.Default;
            changes = database.ChangesSince(0, options, null, null);
            CollectionAssert.AreEqual(new[] { other, conflict }, changes);
            changes = database.ChangesSinceStreaming(0, options, null, null).ToList();
            CollectionAssert.AreEqual(new[] { other, conflict }, changes);
            options.IncludeConflicts = true;
            changes = database.ChangesSince(0, options, null, null);
            expectedChanges = new RevisionList();
            expectedChanges.Add(other);
            expectedChanges.Add(conflict);
            expectedChanges.Add(rev);
            Assert.IsFalse(changes.Except(expectedChanges).Any());
            changes = database.ChangesSinceStreaming(0, options, null, null).ToList();
            Assert.IsFalse(changes.Except(expectedChanges).Any());
        }

        [Test]
        public void TestRevTreeChangeNotification()
        {
            const string DOCUMENT_ID = "MyDocId";

            var rev = new RevisionInternal(DOCUMENT_ID, "1-abcd".AsRevID(), false);
            var revProperties = new Dictionary<string, object>();
            revProperties.SetDocRevID(rev.DocID, rev.RevID);
            revProperties["message"] = "hi";
            rev.SetProperties(revProperties);

            var revHistory = new List<RevisionID>();
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
            var rev3 = new RevisionInternal(DOCUMENT_ID, "3-abcd".AsRevID(), false);
            var rev3Properties = new Dictionary<string, object>();
            rev3Properties.SetDocRevID(rev3.DocID, rev3.RevID);
            rev3Properties["message"] = "hi again";
            rev3.SetProperties(rev3Properties);

            var rev3History = new List<RevisionID>();
            rev3History.Add(rev3.RevID);
            rev3History.Add("2-abcd".AsRevID());
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
            var conflictRev = new RevisionInternal(DOCUMENT_ID, "3-bcde".AsRevID(), false);
            var conflictProperties = new Dictionary<string, object>();
            conflictProperties.SetDocRevID(conflictRev.DocID, conflictRev.RevID);
            conflictProperties["message"] = "winner";
            conflictRev.SetProperties(conflictProperties);

            var conflictRevHistory = new List<RevisionID>();
            conflictRevHistory.Add(conflictRev.RevID);
            conflictRevHistory.Add("2-abcd".AsRevID());
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

       private void VerifyRev(RevisionInternal rev, IList<RevisionID> history)
        {
            var gotRev = database.GetDocument(rev.DocID, null, true);
            Assert.AreEqual(rev, gotRev);
            Assert.AreEqual(rev.GetProperties(), gotRev.GetProperties());

            var revHistory = database.GetRevisionHistory(gotRev, null);
            Assert.AreEqual(history.Count, revHistory.Count);
            for(var i = 0; i < history.Count; i++) {
                Assert.AreEqual(history[i], revHistory[i]);
            }
        }

        private static DocumentChange Announcement(Database db, RevisionInternal rev, RevisionInternal winner)
        {
            Thread.Sleep(200);
            db.GetSequence(rev);
            return new DocumentChange(rev, winner.RevID, false, null);
        }
    }
}

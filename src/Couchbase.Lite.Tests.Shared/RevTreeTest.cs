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
/**
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
    public class RevTreeTest : LiteTestCase
    {
        public const string Tag = "RevTree";

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestForceInsertEmptyHistory()
        {
            var rev = new RevisionInternal("FakeDocId", "1-tango", false, database);
            var revProperties = new Dictionary<string, object>();
            revProperties.Put("_id", rev.GetDocId());
            revProperties.Put("_rev", rev.GetRevId());
            revProperties["message"] = "hi";
            rev.SetProperties(revProperties);

            IList<string> revHistory = null;
            database.ForceInsert(rev, revHistory, null);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestRevTree()
        {
            var rev = new RevisionInternal("MyDocId", "4-foxy", false, database);
            var revProperties = new Dictionary<string, object>();
            revProperties.Put("_id", rev.GetDocId());
            revProperties.Put("_rev", rev.GetRevId());
            revProperties["message"] = "hi";
            rev.SetProperties(revProperties);

            var revHistory = new AList<string>();
            revHistory.AddItem(rev.GetRevId());
            revHistory.AddItem("3-thrice");
            revHistory.AddItem("2-too");
            revHistory.AddItem("1-won");
            database.ForceInsert(rev, revHistory, null);
            Assert.AreEqual(1, database.DocumentCount);

            VerifyHistory(database, rev, revHistory);
            var conflict = new RevisionInternal("MyDocId", "5-epsilon", false, database);
            var conflictProperties = new Dictionary<string, object>();
            conflictProperties.Put("_id", conflict.GetDocId());
            conflictProperties.Put("_rev", conflict.GetRevId());
            conflictProperties["message"] = "yo";
            conflict.SetProperties(conflictProperties);
            
            var conflictHistory = new AList<string>();
            conflictHistory.AddItem(conflict.GetRevId());
            conflictHistory.AddItem("4-delta");
            conflictHistory.AddItem("3-gamma");
            conflictHistory.AddItem("2-too");
            conflictHistory.AddItem("1-won");
            database.ForceInsert(conflict, conflictHistory, null);
            Assert.AreEqual(1, database.DocumentCount);
            VerifyHistory(database, conflict, conflictHistory);
            
            // Add an unrelated document:
            var other = new RevisionInternal("AnotherDocID", "1-ichi", false, database);
            var otherProperties = new Dictionary<string, object>();
            otherProperties["language"] = "jp";
            other.SetProperties(otherProperties);
            var otherHistory = new AList<string>();
            otherHistory.AddItem(other.GetRevId());
            database.ForceInsert(other, otherHistory, null);
            
            // Fetch one of those phantom revisions with no body:
            var rev2 = database.GetDocumentWithIDAndRev(rev.GetDocId(), "2-too", 
                DocumentContentOptions.None);
            Assert.AreEqual(rev.GetDocId(), rev2.GetDocId());
            Assert.AreEqual("2-too", rev2.GetRevId());
            
            // Make sure no duplicate rows were inserted for the common revisions:
            Assert.AreEqual(8, database.GetLastSequenceNumber());
            // Make sure the revision with the higher revID wins the conflict:
            var current = database.GetDocumentWithIDAndRev(rev.GetDocId(), null, 
                DocumentContentOptions.None);
            Assert.AreEqual(conflict, current);
            
            // Get the _changes feed and verify only the winner is in it:
            var options = new ChangesOptions();
            var changes = database.ChangesSince(0, options, null);
            var expectedChanges = new RevisionList();
            expectedChanges.AddItem(conflict);
            expectedChanges.AddItem(other);
            Assert.AreEqual(changes, expectedChanges);
            options.SetIncludeConflicts(true);
            changes = database.ChangesSince(0, options, null);
            expectedChanges = new RevisionList();
            expectedChanges.AddItem(rev);
            expectedChanges.AddItem(conflict);
            expectedChanges.AddItem(other);
            Assert.AreEqual(changes, expectedChanges);
        }

        [Test]
        public void TestRevTreeChangeNotification()
        {
            const string DOCUMENT_ID = "MyDocId";

            var rev = new RevisionInternal(DOCUMENT_ID, "1-one", false, database);
            var revProperties = new Dictionary<string, object>();
            revProperties["_id"] = rev.GetDocId();
            revProperties["_rev"] = rev.GetRevId();
            revProperties["message"] = "hi";
            rev.SetProperties(revProperties);

            var revHistory = new List<string>();
            revHistory.Add(rev.GetRevId());

            EventHandler<Database.DatabaseChangeEventArgs> handler = (sender, e) =>
            {
                var changes = e.Changes.ToList();
                Assert.AreEqual(1, changes.Count);
                var change = changes[0];
                Assert.AreEqual(DOCUMENT_ID, change.DocumentId);
                Assert.AreEqual(rev.GetRevId(), change.RevisionId);
                Assert.IsTrue(change.IsCurrentRevision);
                Assert.IsFalse(change.IsConflict);

                var current = database.GetDocument(change.DocumentId).CurrentRevision;
                Assert.AreEqual(rev.GetRevId(), current.Id);
            };

            database.Changed += handler;
            database.ForceInsert(rev, revHistory, null);
            database.Changed -= handler;

            // add two more revisions to the document
            var rev3 = new RevisionInternal(DOCUMENT_ID, "3-three", false, database);
            var rev3Properties = new Dictionary<string, object>();
            rev3Properties["_id"] = rev3.GetDocId();
            rev3Properties["_rev"] = rev3.GetRevId();
            rev3Properties["message"] = "hi again";
            rev3.SetProperties(rev3Properties);

            var rev3History = new List<string>();
            rev3History.Add(rev3.GetRevId());
            rev3History.Add("2-two");
            rev3History.Add(rev.GetRevId());

            handler = (sender, e) =>
            {
                var changes = e.Changes.ToList();
                Assert.AreEqual(1, changes.Count);
                var change = changes[0];
                Assert.AreEqual(DOCUMENT_ID, change.DocumentId);
                Assert.AreEqual(rev3.GetRevId(), change.RevisionId);
                Assert.IsTrue(change.IsCurrentRevision);
                Assert.IsFalse(change.IsConflict);

                var doc = database.GetDocument(change.DocumentId);
                Assert.AreEqual(rev3.GetRevId(), doc.CurrentRevisionId);
                try
                {
                    Assert.AreEqual(3, doc.RevisionHistory.ToList().Count);
                }
                catch (CouchbaseLiteException ex)
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
            var conflictRev = new RevisionInternal(DOCUMENT_ID, "3-winner", false, database);
            var conflictProperties = new Dictionary<string, object>();
            conflictProperties["_id"] = conflictRev.GetDocId();
            conflictProperties["_rev"] = conflictRev.GetRevId();
            conflictProperties["message"] = "winner";
            conflictRev.SetProperties(conflictProperties);

            var conflictRevHistory = new List<string>();
            conflictRevHistory.Add(conflictRev.GetRevId());
            conflictRevHistory.Add("2-two");
            conflictRevHistory.Add(rev.GetRevId());

            handler = (sender, e) =>
            {
                var changes = e.Changes.ToList();
                Assert.AreEqual(1, changes.Count);
                var change = changes[0];
                Assert.AreEqual(DOCUMENT_ID, change.DocumentId);
                Assert.AreEqual(conflictRev.GetRevId(), change.RevisionId);
                Assert.IsTrue(change.IsCurrentRevision);
                Assert.IsFalse(change.IsConflict);

                var doc = database.GetDocument(change.DocumentId);
                Assert.AreEqual(rev3.GetRevId(), doc.CurrentRevisionId);
                try
                {
                    Assert.AreEqual(2, doc.ConflictingRevisions.ToList().Count);
                    Assert.AreEqual(3, doc.RevisionHistory.ToList().Count);
                }
                catch (CouchbaseLiteException ex)
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
            var gotRev = db.GetDocumentWithIDAndRev(rev.GetDocId(), null, 
                DocumentContentOptions.None);
            Assert.AreEqual(rev, gotRev);
            AssertPropertiesAreEqual(rev.GetProperties(), gotRev.GetProperties());

            var revHistory = db.GetRevisionHistory(gotRev);
            Assert.AreEqual(history.Count, revHistory.Count);
            
            for (int i = 0; i < history.Count; i++)
            {
                RevisionInternal hrev = revHistory[i];
                Assert.AreEqual(rev.GetDocId(), hrev.GetDocId());
                Assert.AreEqual(history[i], hrev.GetRevId());
                Assert.IsFalse(rev.IsDeleted());
            }
        }
    }
}

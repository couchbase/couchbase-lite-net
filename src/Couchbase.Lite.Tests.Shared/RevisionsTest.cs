//
// RevisionsTest.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
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
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Couchbase.Lite.Internal;
using Couchbase.Lite.Revisions;
using NUnit.Framework;

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class RevisionsTest : LiteTestCase
    {
        private static RevisionInternal Mkrev(string revID)
        {
            return new RevisionInternal("docid", revID.AsRevID(), false);
        }

        public RevisionsTest(string storageType) : base(storageType) {}

        [Test]
        public void TestParseRevID()
        {
            var parsed = "1-utiopturoewpt".AsRevID();
            Assert.AreEqual(1, parsed.Generation);
            Assert.AreEqual("utiopturoewpt", parsed.Suffix);

            parsed = "321-fdjfdsj-e".AsRevID();
            Assert.AreEqual(321, parsed.Generation);
            Assert.AreEqual("fdjfdsj-e", parsed.Suffix);

            parsed = "0-fdjfdsj-e".AsRevID();
            Assert.IsTrue(parsed.Generation == 0 && parsed.Suffix == "fdjfdsj-e");
            parsed = "-4-fdjfdsj-e".AsRevID();
            Assert.IsTrue(parsed.Generation < 0);
            parsed = "5_fdjfdsj-e".AsRevID();
            Assert.IsTrue(parsed.Generation < 0);
            parsed = " 5-fdjfdsj-e".AsRevID();
            Assert.IsTrue(parsed.Generation < 0);
            parsed = "7 -foo".AsRevID();
            Assert.IsTrue(parsed.Generation < 0);
            parsed = "7-".AsRevID();
            Assert.IsTrue(parsed.Generation < 0);
            parsed = "7".AsRevID();
           
            Assert.IsTrue(parsed.Generation < 0);
            parsed = "eiuwtiu".AsRevID();
           
            Assert.IsTrue(parsed.Generation < 0);
            parsed = string.Empty.AsRevID();
            Assert.IsTrue(parsed.Generation < 0);
        }

        [Test]
        public void TestCBLCompareRevIDs()
        {
            // Single Digit
            Assert.IsTrue("1-foo".AsRevID().CompareTo("1-foo".AsRevID()) == 0);
            Assert.IsTrue("2-bar".AsRevID().CompareTo("1-foo".AsRevID()) > 0);
            Assert.IsTrue("1-foo".AsRevID().CompareTo("2-bar".AsRevID()) < 0);

            // Multi-digit:
            Assert.IsTrue("123-bar".AsRevID().CompareTo("456-foo".AsRevID()) < 0);
            Assert.IsTrue("456-foo".AsRevID().CompareTo("123-bar".AsRevID()) > 0);
            Assert.IsTrue("456-foo".AsRevID().CompareTo("456-foo".AsRevID()) == 0);
            Assert.IsTrue("456-foo".AsRevID().CompareTo("456-foofoo".AsRevID()) < 0);

            // Different numbers of digits:
            Assert.IsTrue("89-foo".AsRevID().CompareTo("123-bar".AsRevID()) < 0);
            Assert.IsTrue("123-bar".AsRevID().CompareTo("89-foo".AsRevID()) > 0);

            // Edge cases:
            Assert.IsTrue("123-".AsRevID().CompareTo("89-".AsRevID()) > 0);
            Assert.IsTrue("123-a".AsRevID().CompareTo("123-a".AsRevID()) == 0);

            // Invalid rev IDs:
            Assert.IsTrue("-a".AsRevID().CompareTo("-b".AsRevID()) < 0);
            Assert.IsTrue("-".AsRevID().CompareTo("-".AsRevID()) == 0);
            Assert.IsTrue(string.Empty.AsRevID().CompareTo(string.Empty.AsRevID()) == 0);
            Assert.IsTrue(string.Empty.AsRevID().CompareTo("-b".AsRevID()) < 0);
            Assert.IsTrue("bogus".AsRevID().CompareTo("yo".AsRevID()) < 0);
            Assert.IsTrue("bogus-x".AsRevID().CompareTo("yo-y".AsRevID()) < 0);
        }

        [Test]
        public void TestMakeRevisionHistoryDict()
        {
            var revs = new List<RevisionID>();
            revs.Add("4-jkl".AsRevID());
            revs.Add("3-ghi".AsRevID());
            revs.Add("2-def".AsRevID());

            var expectedSuffixes = new List<string>();
            expectedSuffixes.Add("jkl");
            expectedSuffixes.Add("ghi");
            expectedSuffixes.Add("def");

            var expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict["start"] = 4;
            expectedHistoryDict["ids"] = expectedSuffixes;

            var historyDict = TreeRevisionID.MakeRevisionHistoryDict(revs);
            Assert.AreEqual(expectedHistoryDict, historyDict);
            
            revs = new List<RevisionID>();
            revs.Add("4-jkl".AsRevID());
            revs.Add("2-def".AsRevID());
            
            expectedSuffixes = new List<string>();
            expectedSuffixes.Add("4-jkl");
            expectedSuffixes.Add("2-def");
            
            expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict["ids"] = expectedSuffixes;
            historyDict = TreeRevisionID.MakeRevisionHistoryDict(revs);
            Assert.AreEqual(expectedHistoryDict, historyDict);

            revs = new List<RevisionID>();
            revs.Add("12345".AsRevID());
            revs.Add("6789".AsRevID());
            
            expectedSuffixes = new List<string>();
            expectedSuffixes.Add("12345");
            expectedSuffixes.Add("6789");
            
            expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict["ids"] = expectedSuffixes;
            historyDict = TreeRevisionID.MakeRevisionHistoryDict(revs);
            
            Assert.AreEqual(expectedHistoryDict, historyDict);
        }

        [Test]
        public void TestCorrectWinningRevisionHighRevisionNumber() {
            // Create a conflict on purpose
            var doc = database.CreateDocument();
            var rev1 = doc.CreateRevision().Save();
            CreateRevisionWithRandomProps(rev1, false);
            var rev2b = CreateRevisionWithRandomProps(rev1, true);
            var rev3b = CreateRevisionWithRandomProps(rev2b, true);
            var rev4b = CreateRevisionWithRandomProps(rev3b, true);
            var rev5b = CreateRevisionWithRandomProps(rev4b, true);
            var rev6b = CreateRevisionWithRandomProps(rev5b, true);
            var rev7b = CreateRevisionWithRandomProps(rev6b, true);
            var rev8b = CreateRevisionWithRandomProps(rev7b, true);
            var rev9b = CreateRevisionWithRandomProps(rev8b, true);
            var rev10b = CreateRevisionWithRandomProps(rev9b, true);

            var revFound = database.GetDocument(doc.Id, null, true);
            Assert.AreEqual(rev10b.Id, revFound.RevID);
        }

        [Test]
        public void TestCorrectWinningRevisionLongerBranch() {
            // Create a conflict on purpose
            var doc = database.CreateDocument();
            var rev1 = doc.CreateRevision().Save();
            var rev2a = CreateRevisionWithRandomProps(rev1, false);
            Assert.IsNotNull(rev2a);
            var rev2b = CreateRevisionWithRandomProps(rev1, true);
            var rev3b = CreateRevisionWithRandomProps(rev2b, true);

            // rev3b should be picked as the winner since it has a longer branch
            var expectedWinner = rev3b;

            var revFound = database.GetDocument(doc.Id, null, true);
            Assert.AreEqual(expectedWinner.Id, revFound.RevID);
        }

        [Test]
        public void TestCorrectWinningRevisionTiebreaker() {
            // Create a conflict on purpose
            var doc = database.CreateDocument();
            var rev1 = doc.CreateRevision().Save();
            var rev2a = CreateRevisionWithRandomProps(rev1, false);
            var rev2b = CreateRevisionWithRandomProps(rev1, true);

            // the tiebreaker will happen based on which rev hash has lexicographically higher sort order
            SavedRevision expectedWinner = null;
            if (string.Compare(rev2a.Id, rev2b.Id, StringComparison.Ordinal) > 0) {
                expectedWinner = rev2a;
            } else if (string.Compare(rev2a.Id, rev2b.Id, StringComparison.Ordinal) < 0) {
                expectedWinner = rev2b;
            }

            var revFound = database.GetDocument(doc.Id, null, true);
            Assert.AreEqual(expectedWinner.Id, revFound.RevID);
        }

        [Test]
        public void TestDocumentChangeListener() {
            var doc = database.CreateDocument();
            var counter = new CountdownEvent(1);
            doc.Change += (sender, e) => counter.Signal();

            doc.CreateRevision().Save();

            var success = counter.Wait(TimeSpan.FromSeconds(5));
            Assert.IsTrue(success);
        }

        [Test]
        public void TestRevisionIdDifferentRevisions() {
            // two revisions with different json should have different rev-id's
            // because their content will have a different hash (even though
            // they have the same generation number)
            var properties = new Dictionary<string, object>()
            { 
                {"testName", "testCreateRevisions"},
                {"tag", 1337}
            };

            var doc = database.CreateDocument();

            var newRev = doc.CreateRevision();
            newRev.SetUserProperties(properties);
            var rev1 = newRev.Save();
            var rev2a = CreateRevisionWithRandomProps(rev1, false);
            var rev2b = CreateRevisionWithRandomProps(rev1, true);

            Assert.AreNotEqual(rev2a.Id, rev2b.Id);
        }

        [Test]
        public void TestRevisionIdEquivalentRevisions() {
            // two revisions with the same content and the same json
            // should have the exact same revision id, because their content
            // will have an identical hash
            var properties = new Dictionary<string, object>()
            { 
                {"testName", "testCreateRevisions"},
                {"tag", 1337}
            };

            var properties2 = new Dictionary<string, object>()
            { 
                {"testName", "testCreateRevisions"},
                {"tag", 1338}
            };

            var doc = database.CreateDocument();

            var newRev = doc.CreateRevision();
            newRev.SetUserProperties(properties);
            var rev1 = newRev.Save();

            var newRev2a = rev1.CreateRevision();
            newRev2a.SetUserProperties(properties2);
            var rev2a = newRev2a.Save();

            var newRev2b = rev1.CreateRevision();
            newRev2b.SetUserProperties(properties2);
            var rev2b = newRev2b.Save(true);

            Assert.AreEqual(rev2a.Id, rev2b.Id);
        }

        [Test]
        public void TestResolveConflict() {
            var properties = new Dictionary<string, object>()
            { 
                {"testName", "testCreateRevisions"},
                {"tag", 1337}
            };

            // Create a conflict on purpose
            var doc = database.CreateDocument();

            var newRev1 = doc.CreateRevision();
            newRev1.SetUserProperties(properties);
            var rev1 = newRev1.Save();

            var rev2a = CreateRevisionWithRandomProps(rev1, false);
            var rev2b = CreateRevisionWithRandomProps(rev1, true);

            SavedRevision winningRev = null;
            SavedRevision losingRev = null;
            if (doc.CurrentRevisionId.Equals(rev2a.Id))
            {
                winningRev = rev2a;
                losingRev = rev2b;
            } 
            else 
            {
                winningRev = rev2b;
                losingRev = rev2a;
            }

            Assert.AreEqual(2, doc.ConflictingRevisions.Count());
            Assert.AreEqual(2, doc.GetLeafRevisions(true).Count);

            // let's manually choose the losing rev as the winner.  First, delete winner, which will
            // cause losing rev to be the current revision.
            var deleteRevision = winningRev.DeleteDocument();

            Assert.AreEqual(1, doc.ConflictingRevisions.Count());
            Assert.AreEqual(2, doc.GetLeafRevisions(true).Count);
            Assert.AreEqual(3, deleteRevision.Id.AsRevID().Generation);
            Assert.AreEqual(losingRev.Id, doc.CurrentRevisionId);

            // Finally create a new revision rev3 based on losing rev
            var rev3 = CreateRevisionWithRandomProps(losingRev, true);
            Assert.AreEqual(rev3.Id, doc.CurrentRevisionId);
            Assert.AreEqual(1, doc.ConflictingRevisions.Count());
            Assert.AreEqual(2, doc.GetLeafRevisions(true).Count);
        }

        [Test]
        [Description("Handle the case where _deleted is not a bool.  See: https://github.com/couchbase/couchbase-lite-net/issues/414")]
        public void TestRevisionWithNull()
        {

            RevisionInternal revisionWitDeletedNull  = new RevisionInternal(new Dictionary<string, Object>
                            {
                                {"_id", Guid.NewGuid().ToString()},
                                {"_rev", "1-23243234"},
                                {"_deleted", null}
                            });

            RevisionInternal revisionWithDeletedFalse = new RevisionInternal(new Dictionary<string, Object>
                            {
                                {"_id", Guid.NewGuid().ToString()},
                                {"_rev", "1-23243234"},
                                {"_deleted", false}
                            });

            RevisionInternal revisionWithDeletedTrue = new RevisionInternal(new Dictionary<string, Object>
                            {
                                {"_id", Guid.NewGuid().ToString()},
                                {"_rev", "1-23243234"},
                                {"_deleted", true}
                            });

            RevisionInternal revisionWithDeletedString = new RevisionInternal(new Dictionary<string, Object>
                            {
                                {"_id", Guid.NewGuid().ToString()},
                                {"_rev", "1-23243234"},
                                {"_deleted", "foo"}
                            });

            Assert.IsFalse(revisionWitDeletedNull.Deleted);
            Assert.IsFalse(revisionWithDeletedFalse.Deleted);
            Assert.IsFalse(revisionWithDeletedString.Deleted);
            Assert.IsTrue(revisionWithDeletedTrue.Deleted);
        }

    }
}

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
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using NUnit.Framework;
using Sharpen;
using System.Linq;

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class RevisionsTest : LiteTestCase
    {
        private static RevisionInternal Mkrev(string revID)
        {
            return new RevisionInternal("docid", revID, false);
        }

        public RevisionsTest(string storageType) : base(storageType) {}

        [Test]
        public void TestParseRevID()
        {
            var parsed = RevisionInternal.ParseRevId("1-utiopturoewpt");
            Assert.AreEqual(1, parsed.Item1);
            Assert.AreEqual("utiopturoewpt", parsed.Item2);

            parsed = RevisionInternal.ParseRevId("321-fdjfdsj-e");
            Assert.AreEqual(321, parsed.Item1);
            Assert.AreEqual("fdjfdsj-e", parsed.Item2);

            parsed = RevisionInternal.ParseRevId("0-fdjfdsj-e");
            Assert.IsTrue(parsed.Item1 == 0 && parsed.Item2 == "fdjfdsj-e");
            parsed = RevisionInternal.ParseRevId("-4-fdjfdsj-e");
            Assert.IsTrue(parsed.Item1 < 0);
            parsed = RevisionInternal.ParseRevId("5_fdjfdsj-e");
            Assert.IsTrue(parsed.Item1 < 0);
            parsed = RevisionInternal.ParseRevId(" 5-fdjfdsj-e");
            Assert.IsTrue(parsed.Item1 < 0);
            parsed = RevisionInternal.ParseRevId("7 -foo");
            Assert.IsTrue(parsed.Item1 < 0);
            parsed = RevisionInternal.ParseRevId("7-");
            Assert.IsTrue(parsed.Item1 < 0);
            parsed = RevisionInternal.ParseRevId("7");
           
            Assert.IsTrue(parsed.Item1 < 0);
            parsed = RevisionInternal.ParseRevId("eiuwtiu");
           
            Assert.IsTrue(parsed.Item1 < 0);
            parsed = RevisionInternal.ParseRevId(string.Empty);
            Assert.IsTrue(parsed.Item1 < 0);
        }

        [Test]
        public void TestCBLCompareRevIDs()
        {
            // Single Digit
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("1-foo", "1-foo") == 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("2-bar", "1-foo") > 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("1-foo", "2-bar") < 0);

            // Multi-digit:
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("123-bar", "456-foo") < 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("456-foo", "123-bar") > 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("456-foo", "456-foo") == 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("456-foo", "456-foofoo") < 0);

            // Different numbers of digits:
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("89-foo", "123-bar") < 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("123-bar", "89-foo") > 0);

            // Edge cases:
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("123-", "89-") > 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("123-a", "123-a") == 0);

            // Invalid rev IDs:
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("-a", "-b") < 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("-", "-") == 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs(string.Empty, string.Empty) == 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs(string.Empty, "-b") < 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("bogus", "yo") < 0);
            Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("bogus-x", "yo-y") < 0);
        }

        [Test]
        public void TestMakeRevisionHistoryDict()
        {
            var revs = new List<RevisionInternal>();
            revs.AddItem(Mkrev("4-jkl"));
            revs.AddItem(Mkrev("3-ghi"));
            revs.AddItem(Mkrev("2-def"));

            var expectedSuffixes = new List<string>();
            expectedSuffixes.AddItem("jkl");
            expectedSuffixes.AddItem("ghi");
            expectedSuffixes.AddItem("def");

            var expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict["start"] = 4;
            expectedHistoryDict["ids"] = expectedSuffixes;

            var historyDict = Database.MakeRevisionHistoryDict(revs);
            Assert.AreEqual(expectedHistoryDict, historyDict);
            
            revs = new List<RevisionInternal>();
            revs.AddItem(Mkrev("4-jkl"));
            revs.AddItem(Mkrev("2-def"));
            
            expectedSuffixes = new List<string>();
            expectedSuffixes.AddItem("4-jkl");
            expectedSuffixes.AddItem("2-def");
            
            expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict["ids"] = expectedSuffixes;
            historyDict = Database.MakeRevisionHistoryDict(revs);
            Assert.AreEqual(expectedHistoryDict, historyDict);

            revs = new List<RevisionInternal>();
            revs.AddItem(Mkrev("12345"));
            revs.AddItem(Mkrev("6789"));
            
            expectedSuffixes = new List<string>();
            expectedSuffixes.AddItem("12345");
            expectedSuffixes.AddItem("6789");
            
            expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict["ids"] = expectedSuffixes;
            historyDict = Database.MakeRevisionHistoryDict(revs);
            
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
            Assert.AreEqual(rev10b.Id, revFound.GetRevId());
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
            Assert.AreEqual(expectedWinner.Id, revFound.GetRevId());
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
            Assert.AreEqual(expectedWinner.Id, revFound.GetRevId());
        }

        [Test]
        public void TestDocumentChangeListener() {
            var doc = database.CreateDocument();
            var counter = new CountDownLatch(1);
            doc.Change += (sender, e) => counter.CountDown();

            doc.CreateRevision().Save();

            var success = counter.Await(TimeSpan.FromSeconds(5));
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
            Assert.AreEqual(3, RevisionInternal.GenerationFromRevID(deleteRevision.Id));
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

            Assert.IsFalse(revisionWitDeletedNull.IsDeleted());
            Assert.IsFalse(revisionWithDeletedFalse.IsDeleted());
            Assert.IsFalse(revisionWithDeletedString.IsDeleted());
            Assert.IsTrue(revisionWithDeletedTrue.IsDeleted());
        }

    }
}

// 
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
//using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
    public class RevisionsTest : LiteTestCase
    {
        public virtual void TestParseRevID()
        {
            int num;
            string suffix;
            num = Database.ParseRevIDNumber("1-utiopturoewpt");
            NUnit.Framework.Assert.AreEqual(1, num);
            suffix = Database.ParseRevIDSuffix("1-utiopturoewpt");
            NUnit.Framework.Assert.AreEqual("utiopturoewpt", suffix);
            num = Database.ParseRevIDNumber("321-fdjfdsj-e");
            NUnit.Framework.Assert.AreEqual(321, num);
            suffix = Database.ParseRevIDSuffix("321-fdjfdsj-e");
            NUnit.Framework.Assert.AreEqual("fdjfdsj-e", suffix);
            num = Database.ParseRevIDNumber("0-fdjfdsj-e");
            suffix = Database.ParseRevIDSuffix("0-fdjfdsj-e");
            NUnit.Framework.Assert.IsTrue(num == 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("-4-fdjfdsj-e");
            suffix = Database.ParseRevIDSuffix("-4-fdjfdsj-e");
            NUnit.Framework.Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("5_fdjfdsj-e");
            suffix = Database.ParseRevIDSuffix("5_fdjfdsj-e");
            NUnit.Framework.Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber(" 5-fdjfdsj-e");
            suffix = Database.ParseRevIDSuffix(" 5-fdjfdsj-e");
            NUnit.Framework.Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("7 -foo");
            suffix = Database.ParseRevIDSuffix("7 -foo");
            NUnit.Framework.Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("7-");
            suffix = Database.ParseRevIDSuffix("7-");
            NUnit.Framework.Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("7");
            suffix = Database.ParseRevIDSuffix("7");
            NUnit.Framework.Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("eiuwtiu");
            suffix = Database.ParseRevIDSuffix("eiuwtiu");
            NUnit.Framework.Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber(string.Empty);
            suffix = Database.ParseRevIDSuffix(string.Empty);
            NUnit.Framework.Assert.IsTrue(num < 0 || (suffix.Length == 0));
        }

        public virtual void TestCBLCompareRevIDs()
        {
            // Single Digit
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("1-foo", "1-foo")
                 == 0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("2-bar", "1-foo")
                 > 0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("1-foo", "2-bar")
                 < 0);
            // Multi-digit:
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("123-bar", "456-foo"
                ) < 0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("456-foo", "123-bar"
                ) > 0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("456-foo", "456-foo"
                ) == 0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("456-foo", "456-foofoo"
                ) < 0);
            // Different numbers of digits:
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("89-foo", "123-bar"
                ) < 0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("123-bar", "89-foo"
                ) > 0);
            // Edge cases:
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("123-", "89-") > 
                0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("123-a", "123-a")
                 == 0);
            // Invalid rev IDs:
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("-a", "-b") < 0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("-", "-") == 0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs(string.Empty, string.Empty
                ) == 0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs(string.Empty, "-b"
                ) < 0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("bogus", "yo") < 
                0);
            NUnit.Framework.Assert.IsTrue(RevisionInternal.CBLCollateRevIDs("bogus-x", "yo-y"
                ) < 0);
        }

        public virtual void TestMakeRevisionHistoryDict()
        {
            IList<RevisionInternal> revs = new AList<RevisionInternal>();
            revs.AddItem(Mkrev("4-jkl"));
            revs.AddItem(Mkrev("3-ghi"));
            revs.AddItem(Mkrev("2-def"));
            IList<string> expectedSuffixes = new AList<string>();
            expectedSuffixes.AddItem("jkl");
            expectedSuffixes.AddItem("ghi");
            expectedSuffixes.AddItem("def");
            IDictionary<string, object> expectedHistoryDict = new Dictionary<string, object>(
                );
            expectedHistoryDict.Put("start", 4);
            expectedHistoryDict.Put("ids", expectedSuffixes);
            IDictionary<string, object> historyDict = Database.MakeRevisionHistoryDict(revs);
            NUnit.Framework.Assert.AreEqual(expectedHistoryDict, historyDict);
            revs = new AList<RevisionInternal>();
            revs.AddItem(Mkrev("4-jkl"));
            revs.AddItem(Mkrev("2-def"));
            expectedSuffixes = new AList<string>();
            expectedSuffixes.AddItem("4-jkl");
            expectedSuffixes.AddItem("2-def");
            expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict.Put("ids", expectedSuffixes);
            historyDict = Database.MakeRevisionHistoryDict(revs);
            NUnit.Framework.Assert.AreEqual(expectedHistoryDict, historyDict);
            revs = new AList<RevisionInternal>();
            revs.AddItem(Mkrev("12345"));
            revs.AddItem(Mkrev("6789"));
            expectedSuffixes = new AList<string>();
            expectedSuffixes.AddItem("12345");
            expectedSuffixes.AddItem("6789");
            expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict.Put("ids", expectedSuffixes);
            historyDict = Database.MakeRevisionHistoryDict(revs);
            NUnit.Framework.Assert.AreEqual(expectedHistoryDict, historyDict);
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-java-core/issues/164</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestRevisionIdDifferentRevisions()
        {
            // two revisions with different json should have different rev-id's
            // because their content will have a different hash (even though
            // they have the same generation number)
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Put("testName", "testCreateRevisions");
            properties.Put("tag", 1337);
            Document doc = database.CreateDocument();
            UnsavedRevision newRev = doc.CreateRevision();
            newRev.SetUserProperties(properties);
            SavedRevision rev1 = newRev.Save();
            SavedRevision rev2a = CreateRevisionWithRandomProps(rev1, false);
            SavedRevision rev2b = CreateRevisionWithRandomProps(rev1, true);
            NUnit.Framework.Assert.AreNotSame(rev2a.GetId(), rev2b.GetId());
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-java-core/issues/164</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestRevisionIdEquivalentRevisions()
        {
            // two revisions with the same content and the same json
            // should have the exact same revision id, because their content
            // will have an identical hash
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Put("testName", "testCreateRevisions");
            properties.Put("tag", 1337);
            IDictionary<string, object> properties2 = new Dictionary<string, object>();
            properties2.Put("testName", "testCreateRevisions");
            properties2.Put("tag", 1338);
            Document doc = database.CreateDocument();
            UnsavedRevision newRev = doc.CreateRevision();
            newRev.SetUserProperties(properties);
            SavedRevision rev1 = newRev.Save();
            UnsavedRevision newRev2a = rev1.CreateRevision();
            newRev2a.SetUserProperties(properties2);
            SavedRevision rev2a = newRev2a.Save();
            UnsavedRevision newRev2b = rev1.CreateRevision();
            newRev2b.SetUserProperties(properties2);
            SavedRevision rev2b = newRev2b.Save(true);
            NUnit.Framework.Assert.AreEqual(rev2a.GetId(), rev2b.GetId());
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-java-core/issues/106</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestResolveConflict()
        {
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Put("testName", "testCreateRevisions");
            properties.Put("tag", 1337);
            // Create a conflict on purpose
            Document doc = database.CreateDocument();
            UnsavedRevision newRev1 = doc.CreateRevision();
            newRev1.SetUserProperties(properties);
            SavedRevision rev1 = newRev1.Save();
            SavedRevision rev2a = CreateRevisionWithRandomProps(rev1, false);
            SavedRevision rev2b = CreateRevisionWithRandomProps(rev1, true);
            SavedRevision winningRev = null;
            SavedRevision losingRev = null;
            if (doc.GetCurrentRevisionId().Equals(rev2a.GetId()))
            {
                winningRev = rev2a;
                losingRev = rev2b;
            }
            else
            {
                winningRev = rev2b;
                losingRev = rev2a;
            }
            NUnit.Framework.Assert.AreEqual(2, doc.GetConflictingRevisions().Count);
            NUnit.Framework.Assert.AreEqual(2, doc.GetLeafRevisions().Count);
            // let's manually choose the losing rev as the winner.  First, delete winner, which will
            // cause losing rev to be the current revision.
            SavedRevision deleteRevision = winningRev.DeleteDocument();
            IList<SavedRevision> conflictingRevisions = doc.GetConflictingRevisions();
            NUnit.Framework.Assert.AreEqual(1, conflictingRevisions.Count);
            NUnit.Framework.Assert.AreEqual(2, doc.GetLeafRevisions().Count);
            NUnit.Framework.Assert.AreEqual(3, deleteRevision.GetGeneration());
            NUnit.Framework.Assert.AreEqual(losingRev.GetId(), doc.GetCurrentRevision().GetId
                ());
            // Finally create a new revision rev3 based on losing rev
            SavedRevision rev3 = CreateRevisionWithRandomProps(losingRev, true);
            NUnit.Framework.Assert.AreEqual(rev3.GetId(), doc.GetCurrentRevisionId());
            IList<SavedRevision> conflictingRevisions1 = doc.GetConflictingRevisions();
            NUnit.Framework.Assert.AreEqual(1, conflictingRevisions1.Count);
            NUnit.Framework.Assert.AreEqual(2, doc.GetLeafRevisions().Count);
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestCorrectWinningRevisionTiebreaker()
        {
            // Create a conflict on purpose
            Document doc = database.CreateDocument();
            SavedRevision rev1 = doc.CreateRevision().Save();
            SavedRevision rev2a = CreateRevisionWithRandomProps(rev1, false);
            SavedRevision rev2b = CreateRevisionWithRandomProps(rev1, true);
            // the tiebreaker will happen based on which rev hash has lexicographically higher sort order
            SavedRevision expectedWinner = null;
            if (Sharpen.Runtime.CompareOrdinal(rev2a.GetId(), rev2b.GetId()) > 0)
            {
                expectedWinner = rev2a;
            }
            else
            {
                if (Sharpen.Runtime.CompareOrdinal(rev2a.GetId(), rev2b.GetId()) < 0)
                {
                    expectedWinner = rev2b;
                }
            }
            RevisionInternal revFound = database.GetDocumentWithIDAndRev(doc.GetId(), null, EnumSet
                .NoneOf<Database.TDContentOptions>());
            NUnit.Framework.Assert.AreEqual(expectedWinner.GetId(), revFound.GetRevId());
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestCorrectWinningRevisionLongerBranch()
        {
            // Create a conflict on purpose
            Document doc = database.CreateDocument();
            SavedRevision rev1 = doc.CreateRevision().Save();
            SavedRevision rev2a = CreateRevisionWithRandomProps(rev1, false);
            SavedRevision rev2b = CreateRevisionWithRandomProps(rev1, true);
            SavedRevision rev3b = CreateRevisionWithRandomProps(rev2b, true);
            // rev3b should be picked as the winner since it has a longer branch
            SavedRevision expectedWinner = rev3b;
            RevisionInternal revFound = database.GetDocumentWithIDAndRev(doc.GetId(), null, EnumSet
                .NoneOf<Database.TDContentOptions>());
            NUnit.Framework.Assert.AreEqual(expectedWinner.GetId(), revFound.GetRevId());
        }

        /// <summary>https://github.com/couchbase/couchbase-lite-java-core/issues/135</summary>
        /// <exception cref="System.Exception"></exception>
        public virtual void TestCorrectWinningRevisionHighRevisionNumber()
        {
            // Create a conflict on purpose
            Document doc = database.CreateDocument();
            SavedRevision rev1 = doc.CreateRevision().Save();
            SavedRevision rev2a = CreateRevisionWithRandomProps(rev1, false);
            SavedRevision rev2b = CreateRevisionWithRandomProps(rev1, true);
            SavedRevision rev3b = CreateRevisionWithRandomProps(rev2b, true);
            SavedRevision rev4b = CreateRevisionWithRandomProps(rev3b, true);
            SavedRevision rev5b = CreateRevisionWithRandomProps(rev4b, true);
            SavedRevision rev6b = CreateRevisionWithRandomProps(rev5b, true);
            SavedRevision rev7b = CreateRevisionWithRandomProps(rev6b, true);
            SavedRevision rev8b = CreateRevisionWithRandomProps(rev7b, true);
            SavedRevision rev9b = CreateRevisionWithRandomProps(rev8b, true);
            SavedRevision rev10b = CreateRevisionWithRandomProps(rev9b, true);
            RevisionInternal revFound = database.GetDocumentWithIDAndRev(doc.GetId(), null, EnumSet
                .NoneOf<Database.TDContentOptions>());
            NUnit.Framework.Assert.AreEqual(rev10b.GetId(), revFound.GetRevId());
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestDocumentChangeListener()
        {
            Document doc = database.CreateDocument();
            CountDownLatch documentChanged = new CountDownLatch(1);
            doc.AddChangeListener(new _ChangeListener_324(documentChanged));
            doc.CreateRevision().Save();
            bool success = documentChanged.Await(30, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(success);
        }

        private sealed class _ChangeListener_324 : Document.ChangeListener
        {
            public _ChangeListener_324(CountDownLatch documentChanged)
            {
                this.documentChanged = documentChanged;
            }

            public void Changed(Document.ChangeEvent @event)
            {
                DocumentChange docChange = @event.GetChange();
                string msg = "New revision added: %s.  Conflict: %s";
                msg = string.Format(msg, docChange.GetAddedRevision(), docChange.IsConflict());
                Log.D(LiteTestCase.Tag, msg);
                documentChanged.CountDown();
            }

            private readonly CountDownLatch documentChanged;
        }

        private static RevisionInternal Mkrev(string revID)
        {
            return new RevisionInternal("docid", revID, false, null);
        }
    }
}

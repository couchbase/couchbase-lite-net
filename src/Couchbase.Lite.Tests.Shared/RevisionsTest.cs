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

using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
    public class RevisionsTest : LiteTestCase
    {
        [Test]
        public void TestParseRevID()
        {
            int num;
            string suffix;

            num = Database.ParseRevIDNumber("1-utiopturoewpt");
            Assert.AreEqual(1, num);
            suffix = Database.ParseRevIDSuffix("1-utiopturoewpt");
            Assert.AreEqual("utiopturoewpt", suffix);

            num = Database.ParseRevIDNumber("321-fdjfdsj-e");
            Assert.AreEqual(321, num);
            suffix = Database.ParseRevIDSuffix("321-fdjfdsj-e");
            Assert.AreEqual("fdjfdsj-e", suffix);

            num = Database.ParseRevIDNumber("0-fdjfdsj-e");
            suffix = Database.ParseRevIDSuffix("0-fdjfdsj-e");
            Assert.IsTrue(num == 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("-4-fdjfdsj-e");
            suffix = Database.ParseRevIDSuffix("-4-fdjfdsj-e");
            Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("5_fdjfdsj-e");
            suffix = Database.ParseRevIDSuffix("5_fdjfdsj-e");
            Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber(" 5-fdjfdsj-e");
            suffix = Database.ParseRevIDSuffix(" 5-fdjfdsj-e");
            Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("7 -foo");
            suffix = Database.ParseRevIDSuffix("7 -foo");
            Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("7-");
            suffix = Database.ParseRevIDSuffix("7-");
            Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("7");
            suffix = Database.ParseRevIDSuffix("7");
            Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber("eiuwtiu");
            suffix = Database.ParseRevIDSuffix("eiuwtiu");
            Assert.IsTrue(num < 0 || (suffix.Length == 0));
            num = Database.ParseRevIDNumber(string.Empty);
            suffix = Database.ParseRevIDSuffix(string.Empty);
            Assert.IsTrue(num < 0 || (suffix.Length == 0));
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
            var revs = new AList<RevisionInternal>();
            revs.AddItem(Mkrev("4-jkl"));
            revs.AddItem(Mkrev("3-ghi"));
            revs.AddItem(Mkrev("2-def"));

            var expectedSuffixes = new AList<string>();
            expectedSuffixes.AddItem("jkl");
            expectedSuffixes.AddItem("ghi");
            expectedSuffixes.AddItem("def");

            var expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict["start"] = 4;
            expectedHistoryDict["ids"] = expectedSuffixes;
            
            var historyDict = Database.MakeRevisionHistoryDict(revs);
            Assert.AreEqual(expectedHistoryDict, historyDict);
            
            revs = new AList<RevisionInternal>();
            revs.AddItem(Mkrev("4-jkl"));
            revs.AddItem(Mkrev("2-def"));
            
            expectedSuffixes = new AList<string>();
            expectedSuffixes.AddItem("4-jkl");
            expectedSuffixes.AddItem("2-def");
            
            expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict["ids"] = expectedSuffixes;
            historyDict = Database.MakeRevisionHistoryDict(revs);
            Assert.AreEqual(expectedHistoryDict, historyDict);

            revs = new AList<RevisionInternal>();
            revs.AddItem(Mkrev("12345"));
            revs.AddItem(Mkrev("6789"));
            
            expectedSuffixes = new AList<string>();
            expectedSuffixes.AddItem("12345");
            expectedSuffixes.AddItem("6789");
            
            expectedHistoryDict = new Dictionary<string, object>();
            expectedHistoryDict["ids"] = expectedSuffixes;
            historyDict = Database.MakeRevisionHistoryDict(revs);
            
            Assert.AreEqual(expectedHistoryDict, historyDict);
        }

        private static RevisionInternal Mkrev(string revID)
        {
            return new RevisionInternal("docid", revID, false, null);
        }
    }
}

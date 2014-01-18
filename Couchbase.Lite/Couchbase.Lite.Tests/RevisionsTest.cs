/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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
using Couchbase.Lite;
using Couchbase.Lite.Internal;
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
			expectedHistoryDict["start"] = 4;
			expectedHistoryDict["ids"] = expectedSuffixes;
			IDictionary<string, object> historyDict = Database.MakeRevisionHistoryDict(revs);
			NUnit.Framework.Assert.AreEqual(expectedHistoryDict, historyDict);
			revs = new AList<RevisionInternal>();
			revs.AddItem(Mkrev("4-jkl"));
			revs.AddItem(Mkrev("2-def"));
			expectedSuffixes = new AList<string>();
			expectedSuffixes.AddItem("4-jkl");
			expectedSuffixes.AddItem("2-def");
			expectedHistoryDict = new Dictionary<string, object>();
			expectedHistoryDict["ids"] = expectedSuffixes;
			historyDict = Database.MakeRevisionHistoryDict(revs);
			NUnit.Framework.Assert.AreEqual(expectedHistoryDict, historyDict);
			revs = new AList<RevisionInternal>();
			revs.AddItem(Mkrev("12345"));
			revs.AddItem(Mkrev("6789"));
			expectedSuffixes = new AList<string>();
			expectedSuffixes.AddItem("12345");
			expectedSuffixes.AddItem("6789");
			expectedHistoryDict = new Dictionary<string, object>();
			expectedHistoryDict["ids"] = expectedSuffixes;
			historyDict = Database.MakeRevisionHistoryDict(revs);
			NUnit.Framework.Assert.AreEqual(expectedHistoryDict, historyDict);
		}

		private static RevisionInternal Mkrev(string revID)
		{
			return new RevisionInternal("docid", revID, false, null);
		}
	}
}

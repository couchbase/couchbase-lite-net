//
// RevTreeTest.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
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
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite
{
	public class RevTreeTest : LiteTestCase
	{
		public const string Tag = "RevTree";

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestForceInsertEmptyHistory()
		{
			IList<string> revHistory = null;
			RevisionInternal rev = new RevisionInternal("FakeDocId", "1-tango", false, database
				);
			IDictionary<string, object> revProperties = new Dictionary<string, object>();
			revProperties.Put("_id", rev.GetDocId());
			revProperties.Put("_rev", rev.GetRevId());
			revProperties.Put("message", "hi");
			rev.SetProperties(revProperties);
			database.ForceInsert(rev, revHistory, null);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestRevTree()
		{
			RevisionInternal rev = new RevisionInternal("MyDocId", "4-foxy", false, database);
			IDictionary<string, object> revProperties = new Dictionary<string, object>();
			revProperties.Put("_id", rev.GetDocId());
			revProperties.Put("_rev", rev.GetRevId());
			revProperties.Put("message", "hi");
			rev.SetProperties(revProperties);
			IList<string> revHistory = new AList<string>();
			revHistory.AddItem(rev.GetRevId());
			revHistory.AddItem("3-thrice");
			revHistory.AddItem("2-too");
			revHistory.AddItem("1-won");
			database.ForceInsert(rev, revHistory, null);
			NUnit.Framework.Assert.AreEqual(1, database.GetDocumentCount());
			VerifyHistory(database, rev, revHistory);
			RevisionInternal conflict = new RevisionInternal("MyDocId", "5-epsilon", false, database
				);
			IDictionary<string, object> conflictProperties = new Dictionary<string, object>();
			conflictProperties.Put("_id", conflict.GetDocId());
			conflictProperties.Put("_rev", conflict.GetRevId());
			conflictProperties.Put("message", "yo");
			conflict.SetProperties(conflictProperties);
			IList<string> conflictHistory = new AList<string>();
			conflictHistory.AddItem(conflict.GetRevId());
			conflictHistory.AddItem("4-delta");
			conflictHistory.AddItem("3-gamma");
			conflictHistory.AddItem("2-too");
			conflictHistory.AddItem("1-won");
			database.ForceInsert(conflict, conflictHistory, null);
			NUnit.Framework.Assert.AreEqual(1, database.GetDocumentCount());
			VerifyHistory(database, conflict, conflictHistory);
			// Add an unrelated document:
			RevisionInternal other = new RevisionInternal("AnotherDocID", "1-ichi", false, database
				);
			IDictionary<string, object> otherProperties = new Dictionary<string, object>();
			otherProperties.Put("language", "jp");
			other.SetProperties(otherProperties);
			IList<string> otherHistory = new AList<string>();
			otherHistory.AddItem(other.GetRevId());
			database.ForceInsert(other, otherHistory, null);
			// Fetch one of those phantom revisions with no body:
			RevisionInternal rev2 = database.GetDocumentWithIDAndRev(rev.GetDocId(), "2-too", 
				EnumSet.NoneOf<Database.TDContentOptions>());
			NUnit.Framework.Assert.AreEqual(rev.GetDocId(), rev2.GetDocId());
			NUnit.Framework.Assert.AreEqual("2-too", rev2.GetRevId());
			//Assert.assertNull(rev2.getContent());
			// Make sure no duplicate rows were inserted for the common revisions:
			NUnit.Framework.Assert.AreEqual(8, database.GetLastSequenceNumber());
			// Make sure the revision with the higher revID wins the conflict:
			RevisionInternal current = database.GetDocumentWithIDAndRev(rev.GetDocId(), null, 
				EnumSet.NoneOf<Database.TDContentOptions>());
			NUnit.Framework.Assert.AreEqual(conflict, current);
			// Get the _changes feed and verify only the winner is in it:
			ChangesOptions options = new ChangesOptions();
			RevisionList changes = database.ChangesSince(0, options, null);
			RevisionList expectedChanges = new RevisionList();
			expectedChanges.AddItem(conflict);
			expectedChanges.AddItem(other);
			NUnit.Framework.Assert.AreEqual(changes, expectedChanges);
			options.SetIncludeConflicts(true);
			changes = database.ChangesSince(0, options, null);
			expectedChanges = new RevisionList();
			expectedChanges.AddItem(rev);
			expectedChanges.AddItem(conflict);
			expectedChanges.AddItem(other);
			NUnit.Framework.Assert.AreEqual(changes, expectedChanges);
		}

		private static void VerifyHistory(Database db, RevisionInternal rev, IList<string
			> history)
		{
			RevisionInternal gotRev = db.GetDocumentWithIDAndRev(rev.GetDocId(), null, EnumSet
				.NoneOf<Database.TDContentOptions>());
			NUnit.Framework.Assert.AreEqual(rev, gotRev);
			NUnit.Framework.Assert.AreEqual(rev.GetProperties(), gotRev.GetProperties());
			IList<RevisionInternal> revHistory = db.GetRevisionHistory(gotRev);
			NUnit.Framework.Assert.AreEqual(history.Count, revHistory.Count);
			for (int i = 0; i < history.Count; i++)
			{
				RevisionInternal hrev = revHistory[i];
				NUnit.Framework.Assert.AreEqual(rev.GetDocId(), hrev.GetDocId());
				NUnit.Framework.Assert.AreEqual(history[i], hrev.GetRevId());
				NUnit.Framework.Assert.IsFalse(rev.IsDeleted());
			}
		}
	}
}

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
//using System.Collections;
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using NUnit.Framework;
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
			IList wasInConflict = new ArrayList();
			Database.ChangeListener listener = new _ChangeListener_84(wasInConflict);
			database.AddChangeListener(listener);
			database.ForceInsert(conflict, conflictHistory, null);
			NUnit.Framework.Assert.IsTrue(wasInConflict.Count > 0);
			database.RemoveChangeListener(listener);
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

		private sealed class _ChangeListener_84 : Database.ChangeListener
		{
			public _ChangeListener_84(IList wasInConflict)
			{
				this.wasInConflict = wasInConflict;
			}

			public void Changed(Database.ChangeEvent @event)
			{
				if (@event.GetChanges()[0].IsConflict())
				{
					wasInConflict.AddItem(new object());
				}
			}

			private readonly IList wasInConflict;
		}

		/// <summary>
		/// Test that the public API works as expected in change notifications after a rev tree
		/// insertion.
		/// </summary>
		/// <remarks>
		/// Test that the public API works as expected in change notifications after a rev tree
		/// insertion.  See https://github.com/couchbase/couchbase-lite-android-core/pull/27
		/// </remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestRevTreeChangeNotifications()
		{
			string DocumentId = "MyDocId";
			// add a document with a single (first) revision
			RevisionInternal rev = new RevisionInternal(DocumentId, "1-one", false, database);
			IDictionary<string, object> revProperties = new Dictionary<string, object>();
			revProperties.Put("_id", rev.GetDocId());
			revProperties.Put("_rev", rev.GetRevId());
			revProperties.Put("message", "hi");
			rev.SetProperties(revProperties);
			IList<string> revHistory = Arrays.AsList(rev.GetRevId());
			Database.ChangeListener listener = new _ChangeListener_154(this, DocumentId, rev);
			database.AddChangeListener(listener);
			database.ForceInsert(rev, revHistory, null);
			database.RemoveChangeListener(listener);
			// add two more revisions to the document
			RevisionInternal rev3 = new RevisionInternal(DocumentId, "3-three", false, database
				);
			IDictionary<string, object> rev3Properties = new Dictionary<string, object>();
			rev3Properties.Put("_id", rev3.GetDocId());
			rev3Properties.Put("_rev", rev3.GetRevId());
			rev3Properties.Put("message", "hi again");
			rev3.SetProperties(rev3Properties);
			IList<string> rev3History = Arrays.AsList(rev3.GetRevId(), "2-two", rev.GetRevId(
				));
			listener = new _ChangeListener_182(this, DocumentId, rev3);
			database.AddChangeListener(listener);
			database.ForceInsert(rev3, rev3History, null);
			database.RemoveChangeListener(listener);
			// add a conflicting revision, with the same history length as the last revision we
			// inserted. Since this new revision's revID has a higher ASCII sort, it should become the
			// new winning revision.
			RevisionInternal conflictRev = new RevisionInternal(DocumentId, "3-winner", false
				, database);
			IDictionary<string, object> conflictProperties = new Dictionary<string, object>();
			conflictProperties.Put("_id", conflictRev.GetDocId());
			conflictProperties.Put("_rev", conflictRev.GetRevId());
			conflictProperties.Put("message", "winner");
			conflictRev.SetProperties(conflictProperties);
			IList<string> conflictRevHistory = Arrays.AsList(conflictRev.GetRevId(), "2-two", 
				rev.GetRevId());
			listener = new _ChangeListener_217(this, DocumentId, conflictRev);
			database.AddChangeListener(listener);
			database.ForceInsert(conflictRev, conflictRevHistory, null);
			database.RemoveChangeListener(listener);
		}

		private sealed class _ChangeListener_154 : Database.ChangeListener
		{
			public _ChangeListener_154(RevTreeTest _enclosing, string DocumentId, RevisionInternal
				 rev)
			{
				this._enclosing = _enclosing;
				this.DocumentId = DocumentId;
				this.rev = rev;
			}

			public void Changed(Database.ChangeEvent @event)
			{
				NUnit.Framework.Assert.AreEqual(1, @event.GetChanges().Count);
				DocumentChange change = @event.GetChanges()[0];
				NUnit.Framework.Assert.AreEqual(DocumentId, change.GetDocumentId());
				NUnit.Framework.Assert.AreEqual(rev.GetRevId(), change.GetRevisionId());
				NUnit.Framework.Assert.IsTrue(change.IsCurrentRevision());
				NUnit.Framework.Assert.IsFalse(change.IsConflict());
				SavedRevision current = this._enclosing.database.GetDocument(change.GetDocumentId
					()).GetCurrentRevision();
				NUnit.Framework.Assert.AreEqual(rev.GetRevId(), current.GetId());
			}

			private readonly RevTreeTest _enclosing;

			private readonly string DocumentId;

			private readonly RevisionInternal rev;
		}

		private sealed class _ChangeListener_182 : Database.ChangeListener
		{
			public _ChangeListener_182(RevTreeTest _enclosing, string DocumentId, RevisionInternal
				 rev3)
			{
				this._enclosing = _enclosing;
				this.DocumentId = DocumentId;
				this.rev3 = rev3;
			}

			public void Changed(Database.ChangeEvent @event)
			{
				NUnit.Framework.Assert.AreEqual(1, @event.GetChanges().Count);
				DocumentChange change = @event.GetChanges()[0];
				NUnit.Framework.Assert.AreEqual(DocumentId, change.GetDocumentId());
				NUnit.Framework.Assert.AreEqual(rev3.GetRevId(), change.GetRevisionId());
				NUnit.Framework.Assert.IsTrue(change.IsCurrentRevision());
				NUnit.Framework.Assert.IsFalse(change.IsConflict());
				Document doc = this._enclosing.database.GetDocument(change.GetDocumentId());
				NUnit.Framework.Assert.AreEqual(rev3.GetRevId(), doc.GetCurrentRevisionId());
				try
				{
					NUnit.Framework.Assert.AreEqual(3, doc.GetRevisionHistory().Count);
				}
				catch (CouchbaseLiteException ex)
				{
					Assert.Fail("CouchbaseLiteException in change listener: " + ex.ToString());
				}
			}

			private readonly RevTreeTest _enclosing;

			private readonly string DocumentId;

			private readonly RevisionInternal rev3;
		}

		private sealed class _ChangeListener_217 : Database.ChangeListener
		{
			public _ChangeListener_217(RevTreeTest _enclosing, string DocumentId, RevisionInternal
				 conflictRev)
			{
				this._enclosing = _enclosing;
				this.DocumentId = DocumentId;
				this.conflictRev = conflictRev;
			}

			public void Changed(Database.ChangeEvent @event)
			{
				NUnit.Framework.Assert.AreEqual(1, @event.GetChanges().Count);
				DocumentChange change = @event.GetChanges()[0];
				NUnit.Framework.Assert.AreEqual(DocumentId, change.GetDocumentId());
				NUnit.Framework.Assert.AreEqual(conflictRev.GetRevId(), change.GetRevisionId());
				NUnit.Framework.Assert.IsTrue(change.IsCurrentRevision());
				NUnit.Framework.Assert.IsTrue(change.IsConflict());
				Document doc = this._enclosing.database.GetDocument(change.GetDocumentId());
				NUnit.Framework.Assert.AreEqual(conflictRev.GetRevId(), doc.GetCurrentRevisionId(
					));
				try
				{
					NUnit.Framework.Assert.AreEqual(2, doc.GetConflictingRevisions().Count);
					NUnit.Framework.Assert.AreEqual(3, doc.GetRevisionHistory().Count);
				}
				catch (CouchbaseLiteException ex)
				{
					Assert.Fail("CouchbaseLiteException in change listener: " + ex.ToString());
				}
			}

			private readonly RevTreeTest _enclosing;

			private readonly string DocumentId;

			private readonly RevisionInternal conflictRev;
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

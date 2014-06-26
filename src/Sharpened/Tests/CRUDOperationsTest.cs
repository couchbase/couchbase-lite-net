//
// CRUDOperationsTest.cs
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
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
	public class CRUDOperationsTest : LiteTestCase, Database.ChangeListener
	{
		public const string Tag = "CRUDOperations";

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestCRUDOperations()
		{
			database.AddChangeListener(this);
			string privateUUID = database.PrivateUUID();
			string publicUUID = database.PublicUUID();
			Log.V(Tag, "DB private UUID = '" + privateUUID + "', public UUID = '" + publicUUID
				 + "'");
			NUnit.Framework.Assert.IsTrue(privateUUID.Length >= 20);
			NUnit.Framework.Assert.IsTrue(publicUUID.Length >= 20);
			//create a document
			IDictionary<string, object> documentProperties = new Dictionary<string, object>();
			documentProperties.Put("foo", 1);
			documentProperties.Put("bar", false);
			documentProperties.Put("baz", "touch");
			Body body = new Body(documentProperties);
			RevisionInternal rev1 = new RevisionInternal(body, database);
			Status status = new Status();
			rev1 = database.PutRevision(rev1, null, false, status);
			Log.V(Tag, "Created " + rev1);
			NUnit.Framework.Assert.IsTrue(rev1.GetDocId().Length >= 10);
			NUnit.Framework.Assert.IsTrue(rev1.GetRevId().StartsWith("1-"));
			//read it back
			RevisionInternal readRev = database.GetDocumentWithIDAndRev(rev1.GetDocId(), null
				, EnumSet.NoneOf<Database.TDContentOptions>());
			NUnit.Framework.Assert.IsNotNull(readRev);
			IDictionary<string, object> readRevProps = readRev.GetProperties();
			NUnit.Framework.Assert.AreEqual(UserProperties(readRevProps), UserProperties(body
				.GetProperties()));
			//now update it
			documentProperties = readRev.GetProperties();
			documentProperties.Put("status", "updated!");
			body = new Body(documentProperties);
			RevisionInternal rev2 = new RevisionInternal(body, database);
			RevisionInternal rev2input = rev2;
			rev2 = database.PutRevision(rev2, rev1.GetRevId(), false, status);
			Log.V(Tag, "Updated " + rev1);
			NUnit.Framework.Assert.AreEqual(rev1.GetDocId(), rev2.GetDocId());
			NUnit.Framework.Assert.IsTrue(rev2.GetRevId().StartsWith("2-"));
			//read it back
			readRev = database.GetDocumentWithIDAndRev(rev2.GetDocId(), null, EnumSet.NoneOf<
				Database.TDContentOptions>());
			NUnit.Framework.Assert.IsNotNull(readRev);
			NUnit.Framework.Assert.AreEqual(UserProperties(readRev.GetProperties()), UserProperties
				(body.GetProperties()));
			// Try to update the first rev, which should fail:
			bool gotExpectedError = false;
			try
			{
				database.PutRevision(rev2input, rev1.GetRevId(), false, status);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedError = e.GetCBLStatus().GetCode() == Status.Conflict;
			}
			NUnit.Framework.Assert.IsTrue(gotExpectedError);
			// Check the changes feed, with and without filters:
			RevisionList changes = database.ChangesSince(0, null, null);
			Log.V(Tag, "Changes = " + changes);
			NUnit.Framework.Assert.AreEqual(1, changes.Count);
			changes = database.ChangesSince(0, null, new _ReplicationFilter_95());
			NUnit.Framework.Assert.AreEqual(1, changes.Count);
			changes = database.ChangesSince(0, null, new _ReplicationFilter_105());
			NUnit.Framework.Assert.AreEqual(0, changes.Count);
			// Delete it:
			RevisionInternal revD = new RevisionInternal(rev2.GetDocId(), null, true, database
				);
			RevisionInternal revResult = null;
			gotExpectedError = false;
			try
			{
				revResult = database.PutRevision(revD, null, false, status);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedError = e.GetCBLStatus().GetCode() == Status.Conflict;
			}
			NUnit.Framework.Assert.IsTrue(gotExpectedError);
			NUnit.Framework.Assert.IsNull(revResult);
			revD = database.PutRevision(revD, rev2.GetRevId(), false, status);
			NUnit.Framework.Assert.AreEqual(Status.Ok, status.GetCode());
			NUnit.Framework.Assert.AreEqual(revD.GetDocId(), rev2.GetDocId());
			NUnit.Framework.Assert.IsTrue(revD.GetRevId().StartsWith("3-"));
			// Delete nonexistent doc:
			RevisionInternal revFake = new RevisionInternal("fake", null, true, database);
			gotExpectedError = false;
			try
			{
				database.PutRevision(revFake, null, false, status);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedError = e.GetCBLStatus().GetCode() == Status.NotFound;
			}
			NUnit.Framework.Assert.IsTrue(gotExpectedError);
			// Read it back (should fail):
			readRev = database.GetDocumentWithIDAndRev(revD.GetDocId(), null, EnumSet.NoneOf<
				Database.TDContentOptions>());
			NUnit.Framework.Assert.IsNull(readRev);
			// Get Changes feed
			changes = database.ChangesSince(0, null, null);
			NUnit.Framework.Assert.IsTrue(changes.Count == 1);
			// Get Revision History
			IList<RevisionInternal> history = database.GetRevisionHistory(revD);
			NUnit.Framework.Assert.AreEqual(revD, history[0]);
			NUnit.Framework.Assert.AreEqual(rev2, history[1]);
			NUnit.Framework.Assert.AreEqual(rev1, history[2]);
		}

		private sealed class _ReplicationFilter_95 : ReplicationFilter
		{
			public _ReplicationFilter_95()
			{
			}

			public bool Filter(SavedRevision revision, IDictionary<string, object> @params)
			{
				return "updated!".Equals(revision.GetProperties().Get("status"));
			}
		}

		private sealed class _ReplicationFilter_105 : ReplicationFilter
		{
			public _ReplicationFilter_105()
			{
			}

			public bool Filter(SavedRevision revision, IDictionary<string, object> @params)
			{
				return "not updated!".Equals(revision.GetProperties().Get("status"));
			}
		}

		public virtual void Changed(Database.ChangeEvent @event)
		{
			IList<DocumentChange> changes = @event.GetChanges();
			foreach (DocumentChange change in changes)
			{
				RevisionInternal rev = change.GetAddedRevision();
				NUnit.Framework.Assert.IsNotNull(rev);
				NUnit.Framework.Assert.IsNotNull(rev.GetDocId());
				NUnit.Framework.Assert.IsNotNull(rev.GetRevId());
				NUnit.Framework.Assert.AreEqual(rev.GetDocId(), rev.GetProperties().Get("_id"));
				NUnit.Framework.Assert.AreEqual(rev.GetRevId(), rev.GetProperties().Get("_rev"));
			}
		}
	}
}

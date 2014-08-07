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
using System.Linq;
using NUnit.Framework;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
    public class CRUDOperationsTest : LiteTestCase
	{
		public const string Tag = "CRUDOperations";

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
		public void TestCRUDOperations()
		{
            database.Changed += (sender, e) => {
                var changes = e.Changes.ToList();
                foreach (DocumentChange change in changes)
                {
                    var rev = change.AddedRevision;
                    Assert.IsNotNull(rev);
                    Assert.IsNotNull(rev.GetDocId());
                    Assert.IsNotNull(rev.GetRevId());
                    Assert.AreEqual(rev.GetDocId(), rev.GetProperties()["_id"]);
                    Assert.AreEqual(rev.GetDocId(), rev.GetProperties()["_rev"]);
                }
            };

            var privateUUID = database.PrivateUUID();
            var publicUUID = database.PublicUUID();
			Log.V(Tag, "DB private UUID = '" + privateUUID + "', public UUID = '" + 
                publicUUID + "'");
			Assert.IsTrue(privateUUID.Length >= 20);
			Assert.IsTrue(publicUUID.Length >= 20);

			//create a document
            var documentProperties = new Dictionary<string, object>();
			documentProperties["foo"] = 1;
			documentProperties["bar"] = false;
			documentProperties["baz"] = "touch";

            var body = new Body(documentProperties);
            var rev1 = new RevisionInternal(body, database);

            var status = new Status();
			rev1 = database.PutRevision(rev1, null, false, status);
			Log.V(Tag, "Created " + rev1);
			Assert.IsTrue(rev1.GetDocId().Length >= 10);
			Assert.IsTrue(rev1.GetRevId().StartsWith("1-"));

			//read it back
            var readRev = database.GetDocumentWithIDAndRev(rev1.GetDocId(), null, 
                EnumSet.NoneOf<DocumentContentOptions>());
			Assert.IsNotNull(readRev);

            var userReadRevProps = UserProperties(readRev.GetProperties());
            var userBodyProps = UserProperties(body.GetProperties());
            Assert.AreEqual(userReadRevProps.Count, userBodyProps.Count);
            foreach(var key in userReadRevProps.Keys) 
            {
                Assert.AreEqual(userReadRevProps[key], userBodyProps[key]);
            }

			//now update it
            documentProperties = (Dictionary<string, object>)readRev.GetProperties();
			documentProperties["status"] = "updated!";
			body = new Body(documentProperties);
            var rev2 = new RevisionInternal(body, database);
            var rev2input = rev2;
			rev2 = database.PutRevision(rev2, rev1.GetRevId(), false, status);
			Log.V(Tag, "Updated " + rev1);
			Assert.AreEqual(rev1.GetDocId(), rev2.GetDocId());
			Assert.IsTrue(rev2.GetRevId().StartsWith("2-"));

			//read it back
			readRev = database.GetDocumentWithIDAndRev(rev2.GetDocId(), null, 
                EnumSet.NoneOf<DocumentContentOptions>());
			Assert.IsNotNull(readRev);
            Assert.AreEqual(UserProperties(readRev.GetProperties()), UserProperties
                (body.GetProperties()));

			// Try to update the first rev, which should fail:
            var gotExpectedError = false;
			try
			{
				database.PutRevision(rev2input, rev1.GetRevId(), false, status);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedError = e.GetCBLStatus().GetCode() == StatusCode.Conflict;
			}
			Assert.IsTrue(gotExpectedError);

			// Check the changes feed, with and without filters:
            var changeRevisions = database.ChangesSince(0, null, null);

            Log.V(Tag, "Changes = " + changeRevisions);
            Assert.AreEqual(1, changeRevisions.Count);

            changeRevisions = database.ChangesSince(0, null, 
                (revision, items) => "updated!".Equals (revision.Properties.Get("status")));
            Assert.AreEqual(1, changeRevisions.Count);

            changeRevisions = database.ChangesSince(0, null, 
                (revision, items) => "not updated!".Equals (revision.Properties.Get("status")));
            Assert.AreEqual(0, changeRevisions.Count);

			// Delete it:
            var revD = new RevisionInternal(rev2.GetDocId(), null, true, database);
			RevisionInternal revResult = null;
			gotExpectedError = false;
			try
			{
				revResult = database.PutRevision(revD, null, false, status);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedError = e.GetCBLStatus().GetCode() == StatusCode.Conflict;
			}
			Assert.IsTrue(gotExpectedError);
			Assert.IsNull(revResult);

			revD = database.PutRevision(revD, rev2.GetRevId(), false, status);
			Assert.AreEqual(StatusCode.Ok, status.GetCode());
			Assert.AreEqual(revD.GetDocId(), rev2.GetDocId());
			Assert.IsTrue(revD.GetRevId().StartsWith("3-"));
			
            // Delete nonexistent doc:
            var revFake = new RevisionInternal("fake", null, true, database);
			gotExpectedError = false;
			try
			{
				database.PutRevision(revFake, null, false, status);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedError = e.GetCBLStatus().GetCode() == StatusCode.NotFound;
			}
			Assert.IsTrue(gotExpectedError);

			// Read it back (should fail):
			readRev = database.GetDocumentWithIDAndRev(revD.GetDocId(), null, 
                EnumSet.NoneOf<DocumentContentOptions>());
			Assert.IsNull(readRev);

            // Get Changes feed:
            changeRevisions = database.ChangesSince(0, null, null);
            Assert.IsTrue(changeRevisions.Count == 1);

            // Get Revision History:
			IList<RevisionInternal> history = database.GetRevisionHistory(revD);
			Assert.AreEqual(revD, history[0]);
			Assert.AreEqual(rev2, history[1]);
			Assert.AreEqual(rev1, history[2]);
		}
	}
}

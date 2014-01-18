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
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
	public class LocalDocsTest : LiteTestCase
	{
		public const string Tag = "LocalDocs";

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestLocalDocs()
		{
			//create a document
			IDictionary<string, object> documentProperties = new Dictionary<string, object>();
			documentProperties.Put("_id", "_local/doc1");
			documentProperties["foo"] = 1;
			documentProperties["bar"] = false;
			Body body = new Body(documentProperties);
			RevisionInternal rev1 = new RevisionInternal(body, database);
			Status status = new Status();
			rev1 = database.PutLocalRevision(rev1, null);
			Log.V(Tag, "Created " + rev1);
			NUnit.Framework.Assert.AreEqual("_local/doc1", rev1.GetDocId());
			NUnit.Framework.Assert.IsTrue(rev1.GetRevId().StartsWith("1-"));
			//read it back
			RevisionInternal readRev = database.GetLocalDocument(rev1.GetDocId(), null);
			NUnit.Framework.Assert.IsNotNull(readRev);
            IDictionary<string, object> readRevProps = readRev.Properties;
			NUnit.Framework.Assert.AreEqual(rev1.GetDocId(), readRev.Properties.Get("_id"
				));
			NUnit.Framework.Assert.AreEqual(rev1.GetRevId(), readRev.Properties.Get("_rev"
				));
			NUnit.Framework.Assert.AreEqual(UserProperties(readRevProps), UserProperties(body
				.Properties));
			//now update it
            documentProperties = readRev.Properties;
			documentProperties.Put("status", "updated!");
			body = new Body(documentProperties);
			RevisionInternal rev2 = new RevisionInternal(body, database);
			RevisionInternal rev2input = rev2;
			rev2 = database.PutLocalRevision(rev2, rev1.GetRevId());
			Log.V(Tag, "Updated " + rev1);
			NUnit.Framework.Assert.AreEqual(rev1.GetDocId(), rev2.GetDocId());
			NUnit.Framework.Assert.IsTrue(rev2.GetRevId().StartsWith("2-"));
			//read it back
			readRev = database.GetLocalDocument(rev2.GetDocId(), null);
			NUnit.Framework.Assert.IsNotNull(readRev);
			NUnit.Framework.Assert.AreEqual(UserProperties(readRev.Properties), UserProperties
				(body.Properties));
			// Try to update the first rev, which should fail:
			bool gotException = false;
			try
			{
				database.PutLocalRevision(rev2input, rev1.GetRevId());
			}
			catch (CouchbaseLiteException e)
			{
                NUnit.Framework.Assert.AreEqual(StatusCode.Conflict, e.GetCBLStatus().GetCode());
				gotException = true;
			}
			NUnit.Framework.Assert.IsTrue(gotException);
			// Delete it:
			RevisionInternal revD = new RevisionInternal(rev2.GetDocId(), null, true, database
				);
			gotException = false;
			try
			{
				RevisionInternal revResult = database.PutLocalRevision(revD, null);
				NUnit.Framework.Assert.IsNull(revResult);
			}
			catch (CouchbaseLiteException e)
			{
                NUnit.Framework.Assert.AreEqual(StatusCode.Conflict, e.GetCBLStatus().GetCode());
				gotException = true;
			}
			NUnit.Framework.Assert.IsTrue(gotException);
			revD = database.PutLocalRevision(revD, rev2.GetRevId());
			// Delete nonexistent doc:
			gotException = false;
			RevisionInternal revFake = new RevisionInternal("_local/fake", null, true, database
				);
			try
			{
				database.PutLocalRevision(revFake, null);
			}
			catch (CouchbaseLiteException e)
			{
                NUnit.Framework.Assert.AreEqual(StatusCode.Conflict, e.GetCBLStatus().GetCode());
				gotException = true;
			}
			NUnit.Framework.Assert.IsTrue(gotException);
			// Read it back (should fail):
			readRev = database.GetLocalDocument(revD.GetDocId(), null);
			NUnit.Framework.Assert.IsNull(readRev);
		}
	}
}

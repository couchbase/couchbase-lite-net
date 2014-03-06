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
	public class DocumentTest : LiteTestCase
	{
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestNewDocumentHasCurrentRevision()
		{
			Document document = database.CreateDocument();
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("foo", "foo");
			properties.Put("bar", false);
			document.PutProperties(properties);
			NUnit.Framework.Assert.IsNotNull(document.GetCurrentRevisionId());
			NUnit.Framework.Assert.IsNotNull(document.GetCurrentRevision());
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestDeleteDocument()
		{
			Document document = database.CreateDocument();
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("foo", "foo");
			properties.Put("bar", false);
			document.PutProperties(properties);
			NUnit.Framework.Assert.IsNotNull(document.GetCurrentRevision());
			string docId = document.GetId();
			document.Delete();
			NUnit.Framework.Assert.IsTrue(document.IsDeleted());
			Document fetchedDoc = database.GetExistingDocument(docId);
			NUnit.Framework.Assert.IsNull(fetchedDoc);
			// query all docs and make sure we don't see that document
			database.GetAllDocs(new QueryOptions());
			Query queryAllDocs = database.CreateAllDocumentsQuery();
			QueryEnumerator queryEnumerator = queryAllDocs.Run();
			for (IEnumerator<QueryRow> it = queryEnumerator; it.HasNext(); )
			{
				QueryRow row = it.Next();
				NUnit.Framework.Assert.IsFalse(row.GetDocument().GetId().Equals(docId));
			}
		}

		// Reproduces issue #167
		// https://github.com/couchbase/couchbase-lite-android/issues/167
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestLoadRevisionBody()
		{
			Document document = database.CreateDocument();
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("foo", "foo");
			properties.Put("bar", false);
			document.PutProperties(properties);
			NUnit.Framework.Assert.IsNotNull(document.GetCurrentRevision());
			bool deleted = false;
			RevisionInternal revisionInternal = new RevisionInternal(document.GetId(), document
				.GetCurrentRevisionId(), deleted, database);
			EnumSet<Database.TDContentOptions> contentOptions = EnumSet.Of(Database.TDContentOptions
				.TDIncludeAttachments, Database.TDContentOptions.TDBigAttachmentsFollow);
			database.LoadRevisionBody(revisionInternal, contentOptions);
			// now lets purge the document, and then try to load the revision body again
			NUnit.Framework.Assert.IsTrue(document.Purge());
			bool gotExpectedException = false;
			try
			{
				database.LoadRevisionBody(revisionInternal, contentOptions);
			}
			catch (CouchbaseLiteException e)
			{
				if (e.GetCBLStatus().GetCode() == Status.NotFound)
				{
					gotExpectedException = true;
				}
			}
			NUnit.Framework.Assert.IsTrue(gotExpectedException);
		}
	}
}

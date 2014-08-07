//
// DocumentTest.cs
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
using NUnit.Framework;
using Sharpen;
using Couchbase.Lite.Internal;

namespace Couchbase.Lite
{
	public class DocumentTest : LiteTestCase
	{
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestNewDocumentHasCurrentRevision()
		{
            var document = database.CreateDocument();
            var properties = new Dictionary<string, object>();
			properties["foo"] = "foo";
			properties["bar"] = false;
			document.PutProperties(properties);
            Assert.IsNotNull(document.CurrentRevisionId);
			Assert.IsNotNull(document.CurrentRevision);
		}

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestPutDeletedDocument() 
        {
            Document document = database.CreateDocument();
            var properties = new Dictionary<string, object>();
            properties["foo"] = "foo";
            properties["bar"] = false;
            document.PutProperties(properties);
            Assert.IsNotNull(document.CurrentRevision);

            var docId = document.Id;

            properties["_rev"] = document.CurrentRevisionId;
            properties["_deleted"] = true;
            properties["mykey"] = "myval";
            var newRev = document.PutProperties(properties);
            newRev.LoadProperties();

            Assert.IsTrue(newRev.Properties.ContainsKey("mykey"));
            Assert.IsTrue(document.Deleted);
            var featchedDoc = database.GetExistingDocument(docId);
            Assert.IsNull(featchedDoc);

            var queryAllDocs = database.CreateAllDocumentsQuery();
            var queryEnumerator = queryAllDocs.Run();
            foreach(QueryRow row in queryEnumerator)
            {
                Assert.AreNotEqual(row.Document.Id, docId);
            }
        }

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestDeleteDocument()
		{
            var document = database.CreateDocument();
            var properties = new Dictionary<string, object>();
			properties["foo"] = "foo";
			properties["bar"] = false;
			document.PutProperties(properties);
			Assert.IsNotNull(document.CurrentRevision);

            var docId = document.Id;
			document.Delete();
            Assert.IsTrue(document.Deleted);
			Document fetchedDoc = database.GetExistingDocument(docId);
			Assert.IsNull(fetchedDoc);

			// query all docs and make sure we don't see that document
			database.GetAllDocs(new QueryOptions());
			Query queryAllDocs = database.CreateAllDocumentsQuery();
			QueryEnumerator queryEnumerator = queryAllDocs.Run();
			for (IEnumerator<QueryRow> it = queryEnumerator; it.MoveNext();)
			{
				QueryRow row = it.Current;
                Assert.IsFalse(row.Document.Id.Equals(docId));
			}
		}

        [Test]
        public void TestLoadRevisionBody()
        {
            var document = database.CreateDocument();
            var properties = new Dictionary<string, object>();
            properties["foo"] = "foo";
            properties["bar"] = false;
            document.PutProperties(properties);
            Assert.IsNotNull(document.CurrentRevision);

            var deleted = false;

            var revisionInternal = new RevisionInternal(
                document.Id, document.CurrentRevisionId, deleted, database);

            var contentOptions = EnumSet.Of (DocumentContentOptions.IncludeAttachments, 
                DocumentContentOptions.BigAttachmentsFollow);

            database.LoadRevisionBody(revisionInternal, contentOptions);

            // now lets purge the document, and then try to load the revision body again
            document.Purge();

            var gotExpectedException = false;
            try {
                database.LoadRevisionBody(revisionInternal, contentOptions);
            } catch (CouchbaseLiteException e) {
                gotExpectedException |= 
                    e.GetCBLStatus().GetCode() == StatusCode.NotFound;
            }

            Assert.IsTrue(gotExpectedException);
        }

	}
}

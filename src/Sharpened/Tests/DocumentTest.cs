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

        /// <summary>
        /// Port test over from:
        /// https://github.com/couchbase/couchbase-lite-ios/commit/e0469300672a2087feb46b84ca498facd49e0066
        /// </summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestGetNonExistentDocument()
        {
            NUnit.Framework.Assert.IsNull(database.GetExistingDocument("missing"));
            Document doc = database.GetDocument("missing");
            NUnit.Framework.Assert.IsNotNull(doc);
            NUnit.Framework.Assert.IsNull(database.GetExistingDocument("missing"));
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
            document.Purge();
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

        /// <summary>https://github.com/couchbase/couchbase-lite-android/issues/281</summary>
        public virtual void TestDocumentWithRemovedProperty()
        {
            IDictionary<string, object> props = new Dictionary<string, object>();
            props.Put("_id", "fakeid");
            props.Put("_removed", true);
            props.Put("foo", "bar");
            Document doc = CreateDocumentWithProperties(database, props);
            NUnit.Framework.Assert.IsNotNull(doc);
            Document docFetched = database.GetDocument(doc.GetId());
            IDictionary<string, object> fetchedProps = docFetched.GetCurrentRevision().GetProperties
                ();
            NUnit.Framework.Assert.IsNotNull(fetchedProps.Get("_removed"));
            NUnit.Framework.Assert.IsTrue(docFetched.GetCurrentRevision().IsGone());
        }
    }
}

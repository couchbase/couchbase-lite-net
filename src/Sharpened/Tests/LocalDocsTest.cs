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
            documentProperties.Put("foo", 1);
            documentProperties.Put("bar", false);
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
            IDictionary<string, object> readRevProps = readRev.GetProperties();
            NUnit.Framework.Assert.AreEqual(rev1.GetDocId(), readRev.GetProperties().Get("_id"
                ));
            NUnit.Framework.Assert.AreEqual(rev1.GetRevId(), readRev.GetProperties().Get("_rev"
                ));
            NUnit.Framework.Assert.AreEqual(UserProperties(readRevProps), UserProperties(body
                .GetProperties()));
            //now update it
            documentProperties = readRev.GetProperties();
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
            NUnit.Framework.Assert.AreEqual(UserProperties(readRev.GetProperties()), UserProperties
                (body.GetProperties()));
            // Try to update the first rev, which should fail:
            bool gotException = false;
            try
            {
                database.PutLocalRevision(rev2input, rev1.GetRevId());
            }
            catch (CouchbaseLiteException e)
            {
                NUnit.Framework.Assert.AreEqual(Status.Conflict, e.GetCBLStatus().GetCode());
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
                NUnit.Framework.Assert.AreEqual(Status.Conflict, e.GetCBLStatus().GetCode());
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
                NUnit.Framework.Assert.AreEqual(Status.NotFound, e.GetCBLStatus().GetCode());
                gotException = true;
            }
            NUnit.Framework.Assert.IsTrue(gotException);
            // Read it back (should fail):
            readRev = database.GetLocalDocument(revD.GetDocId(), null);
            NUnit.Framework.Assert.IsNull(readRev);
        }
    }
}

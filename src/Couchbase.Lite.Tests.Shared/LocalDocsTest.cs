//
// LocalDocsTest.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
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
/*
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
using NUnit.Framework;
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
        [Test]
        public void TestLocalDocs()
        {
            //create a document
            var documentProperties = new Dictionary<string, object>();
            documentProperties["_id"] = "_local/doc1";
            documentProperties["foo"] = 1;
            documentProperties["bar"] = false;
            var body = new Body(documentProperties);
            var rev1 = new RevisionInternal(body, database);
            rev1 = database.PutLocalRevision(rev1, null);
            Log.V(Tag, "Created " + rev1);
            Assert.AreEqual("_local/doc1", rev1.GetDocId());
            Assert.IsTrue(rev1.GetRevId().StartsWith("1-"));
            
            //read it back
            var readRev = database.GetLocalDocument(rev1.GetDocId(), null);
            Assert.IsNotNull(readRev);
            var readRevProps = readRev.GetProperties();
            Assert.AreEqual(rev1.GetDocId(), readRevProps.Get("_id"));
            Assert.AreEqual(rev1.GetRevId(), readRevProps.Get("_rev"));
            AssertPropertiesAreEqual(UserProperties(readRevProps), 
                UserProperties(body.GetProperties()));

            //now update it
            documentProperties = (Dictionary<string, object>)readRev.GetProperties();
            documentProperties["status"] = "updated!";
            body = new Body(documentProperties);
            var rev2 = new RevisionInternal(body, database);
            var rev2input = rev2;
            rev2 = database.PutLocalRevision(rev2, rev1.GetRevId());
            Log.V(Tag, "Updated " + rev1);
            Assert.AreEqual(rev1.GetDocId(), rev2.GetDocId());
            Assert.IsTrue(rev2.GetRevId().StartsWith("2-"));
            
            //read it back
            readRev = database.GetLocalDocument(rev2.GetDocId(), null);
            Assert.IsNotNull(readRev);
            AssertPropertiesAreEqual(UserProperties(readRev.GetProperties()), 
                UserProperties(body.GetProperties()));

            // Try to update the first rev, which should fail:
            var gotException = false;
            try
            {
                database.PutLocalRevision(rev2input, rev1.GetRevId());
            }
            catch (CouchbaseLiteException e)
            {
                Assert.AreEqual(StatusCode.Conflict, e.GetCBLStatus().GetCode());
                gotException = true;
            }
            Assert.IsTrue(gotException);
            
            // Delete it:
            var revD = new RevisionInternal(rev2.GetDocId(), null, true, database);
            gotException = false;
            try
            {
                var revResult = database.PutLocalRevision(revD, null);
                Assert.IsNull(revResult);
            }
            catch (CouchbaseLiteException e)
            {
                Assert.AreEqual(StatusCode.Conflict, e.GetCBLStatus().GetCode());
                gotException = true;
            }
            Assert.IsTrue(gotException);
            revD = database.PutLocalRevision(revD, rev2.GetRevId());
            
            // Delete nonexistent doc:
            gotException = false;
            var revFake = new RevisionInternal("_local/fake", null, true, database);
            try
            {
                database.PutLocalRevision(revFake, null);
            }
            catch (CouchbaseLiteException e)
            {
                Assert.AreEqual(StatusCode.NotFound, e.GetCBLStatus().GetCode());
                gotException = true;
            }
            Assert.IsTrue(gotException);
            
            // Read it back (should fail):
            readRev = database.GetLocalDocument(revD.GetDocId(), null);
            Assert.IsNull(readRev);
        }
    }
}

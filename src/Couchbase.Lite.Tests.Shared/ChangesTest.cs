//
// ChangesTest.cs
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
using NUnit.Framework;
using Sharpen;
using System;

namespace Couchbase.Lite
{
    public class ChangesTest : LiteTestCase
    {
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Test]
        public void TestChangeNotification()
        {
            var changeNotifications = 0;

            EventHandler<Database.DatabaseChangeEventArgs> handler
                = (sender, e) => changeNotifications++;

            database.Changed += handler;

            // create a document
            var documentProperties = new Dictionary<string, object>();
            documentProperties["foo"] = 1;
            documentProperties["bar"] = false;
            documentProperties["baz"] = "touch";
            
            var body = new Body(documentProperties);
            var rev1 = new RevisionInternal(body, database);
            
            var status = new Status();
            database.PutRevision(rev1, null, false, status);
            
            Assert.AreEqual(1, changeNotifications);

            // Analysis disable once DelegateSubtraction
            database.Changed -= handler;
        }

        [Test]
        public void TestLocalChangesAreNotExternal()
        {
            var changeNotifications = 0;

            EventHandler<Database.DatabaseChangeEventArgs> handler = (sender, e) =>
            {
                changeNotifications++;
                Assert.IsFalse(e.IsExternal);
            };

            database.Changed += handler;

            // Insert a document locally.
            var document = database.CreateDocument();
            document.CreateRevision().Save();

            // Make sure that the assertion in changeListener was called.
            Assert.AreEqual(1, changeNotifications);

            // Analysis disable once DelegateSubtraction
            database.Changed -= handler;
        }

        [Test]
        public void TestPulledChangesAreExternal()
        {
            var changeNotifications = 0;

            EventHandler<Database.DatabaseChangeEventArgs> handler = (sender, e) =>
            {
                changeNotifications++;
                Assert.IsTrue(e.IsExternal);
            };

            database.Changed += handler;

            // Insert a dcoument as if it came from a remote source.
            var rev = new RevisionInternal("docId", "1-rev", false, database);
            var properties = new Dictionary<string, object>();
            properties["_id"] = rev.GetDocId();
            properties["_rev"] = rev.GetRevId();
            rev.SetProperties(properties);

            var history = new List<string>();
            history.Add(rev.GetRevId());
            database.ForceInsert(rev, history, GetReplicationURL());

            Assert.AreEqual(1, changeNotifications);

            // Analysis disable once DelegateSubtraction
            database.Changed -= handler;
        }
    }
}

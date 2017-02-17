//
// ManagerTest.cs
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Couchbase.Lite.Db;
using NUnit.Framework;
using Couchbase.Lite.Storage.SQLCipher;
using Couchbase.Lite.Internal;

namespace Couchbase.Lite
{
    [TestFixture("ForestDB")]
    public class ManagerTest : LiteTestCase
    {

        public ManagerTest(string storageType) : base(storageType) {}

        [Test]
        public void TestDeleteDatabase()
        {
            var db = manager.GetDatabase("deleteme");
            db.Close();
            manager.DeleteDatabase("deleteme");
            Assert.IsNull(manager.GetExistingDatabase("deleteme"));
        }

        [Test]
        public void TestServer()
        {
            //to ensure this test is easily repeatable we will explicitly remove
            //any stale foo.cblite
            var mustExist = true;
            Database old = manager.GetDatabase("foo", mustExist);
            if (old != null)
            {
                old.Delete();
            }

            mustExist = false;
            var db = manager.GetDatabase("foo", mustExist);
            Assert.IsNotNull(db);
            Assert.AreEqual("foo", db.Name);
            Assert.IsTrue(db.DbDirectory.StartsWith(GetServerPath()));
            Assert.IsFalse(db.Exists());

            // because foo doesn't exist yet
            List<string> databaseNames = manager.AllDatabaseNames.ToList();

            Assert.IsTrue(!databaseNames.Contains("foo"));
            Assert.DoesNotThrow(db.Open);
            Assert.IsTrue(db.Exists());
            
            databaseNames = manager.AllDatabaseNames.ToList();
            Assert.IsTrue(databaseNames.Contains("foo"));
            db.Close();
            db.Delete();
        }

        [Test]
        public void TestReplaceWithPreviousDatabase()
        {
            if (_storageType == "SQLite") {
                TestReplaceDatabase ("net", "120", 1, "doc1", "doc2", "_local/local1");
                TestReplaceDatabase ("net", "130", 1, "doc1", "doc2", "_local/local1");
            } else {
                TestReplaceDatabase ("net", "120-forestdb", 1, "doc1", "doc2", "_local/local1");
                TestReplaceDatabase ("net", "130-forestdb", 1, "doc1", "doc2", "_local/local1");
            }
        }


        [Test]
        public void TestReplaceWithIosDatabase() {
            if (_storageType == "SQLite") {
                TestReplaceDatabase("ios", "120", 2, "doc1");
                TestReplaceDatabase("ios", "130", 1, "doc1", "doc2", "_local/local1");
                TestReplaceDatabase ("ios", "140", 1, "doc1", "doc2", "_local/local1");
            } else {
                TestReplaceDatabase("ios", "120-forestdb", 1, "doc1", "doc2");
                TestReplaceDatabase("ios", "130-forestdb", 1, "doc1", "doc2");
                TestReplaceDatabase ("ios", "140-forestdb", 1, "doc1", "doc2");
            }
        }

        [Test]
        public void TestReplaceWithAndroidDatabase() {
            if (_storageType == "SQLite") {
                TestReplaceDatabase("android", "120", 1, "doc1", "doc2", "_local/local1");
                TestReplaceDatabase("android", "130", 1, "doc1", "doc2", "_local/local1");
                TestReplaceDatabase("android", "140", 1, "doc1", "doc2", "_local/local1");
            } else {
                TestReplaceDatabase("android", "120-forestdb", 1, "doc1", "doc2");
                TestReplaceDatabase("android", "130-forestdb", 1, "doc1", "doc2");
                TestReplaceDatabase ("android", "140-forestdb", 1, "doc1", "doc2");
            }
        }

        [Test]
        public void TestReplaceFailure()
        {
            var doc = database.CreateDocument();
            doc.PutProperties(new Dictionary<string, object> {
                { "foo", "bar" }
            });
                
            Assert.Throws(typeof(ArgumentException), () =>
            {
                using (var assetStream = GetAsset("android120.zip")) {
                    manager.ReplaceDatabase(database.Name, assetStream, false);
                }
            });

            // Verify that the original DB is intact
            doc = database.GetExistingDocument(doc.Id);
            Assert.IsNotNull(doc, "Failed to get original document");
            Assert.AreEqual("bar", doc.UserProperties["foo"]);
        }

        private void TestReplaceDatabase(string platform, string version, int attachmentCount, params string[] docNames)
        {
            using(var assetStream = GetAsset($"{platform}{version}.zip")) {
                if(assetStream == null) {
                    WriteDebug ($"Missing database file {platform}{version}.zip");
                    return;
                }
                manager.ReplaceDatabase($"{platform}db", assetStream, true);
            }

            var db = manager.GetExistingDatabase($"{platform}db");
            Assert.IsNotNull(db, "Failed to import database");
            foreach(var docName in docNames) {
                if(docName.StartsWith("_local/")) {
                    Assert.IsNotNull(db.GetExistingLocalDocument(docName.Substring(7)));
                } else {
                    var doc = db.GetExistingDocument(docName);
                    Assert.IsNotNull(doc, $"Failed to get doc {docName} from imported database");
                    Assert.AreEqual(doc.CurrentRevision.AttachmentNames.Count(), attachmentCount, "Failed to get attachments from imported database");
                    using(var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                        Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                    }
                }
            }

            var view = db.GetView("view");
            view.SetMap((d, emit) =>
            {
                if(d.CblID().Equals(docNames[0])) {
                    emit(d.CblID(), null);
                }
            }, "1");
            var result = view.CreateQuery().Run();
            Assert.AreEqual(1, result.Count);

            db.Dispose();
        }
    }
}

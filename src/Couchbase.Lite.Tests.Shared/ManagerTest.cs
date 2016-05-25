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

        /// <exception cref="System.Exception"></exception>
        [Test]
        public void TestUpgradeOldDatabaseFiles()
        {
            var testDirName = "test-directory-" + DateTime.UtcNow.MillisecondsSinceEpoch();
            var rootDirPath = RootDirectory.FullName;
            var testDirPath = Path.Combine(rootDirPath, testDirName);
            var testDirInfo = Directory.CreateDirectory(testDirPath);

            var dbStream = GetAsset("withattachments.cblite");
            var destStream = File.OpenWrite(Path.Combine(testDirPath, "withattachments" + Manager.DatabaseSuffixv1));
            dbStream.CopyTo(destStream);
            dbStream.Dispose();
            destStream.Dispose();

            var attStream = GetAsset("attachment.blob");
            Directory.CreateDirectory(Path.Combine(testDirPath, "withattachments/attachments"));
            destStream = File.OpenWrite(Path.Combine(testDirPath, "withattachments/attachments/356a192b7913b04c54574d18c28d46e6395428ab.blob"));
            attStream.CopyTo(destStream);
            destStream.Dispose();
            attStream.Dispose();

            StopCBLite();
            manager = new Manager(testDirInfo, Manager.DefaultOptions);
            var db = manager.GetDatabase("withattachments", true);
            int version = DatabaseUpgraderFactory.SchemaVersion(Path.Combine(db.DbDirectory, "db.sqlite3"));
            Assert.IsTrue(version >= 101, "Upgrade failed");
            Assert.IsFalse(Directory.Exists(Path.Combine(testDirPath, "withattachments/attachments")), "Failed to remove old attachments dir");
            Assert.IsTrue(Directory.Exists(db.AttachmentStorePath), "Failed to create new attachments dir");
        }

        [Test]
        public void TestReplaceDatabaseNamedNoAttachments() {
            //Copy database from assets to local storage
            var dbStream = GetAsset("noattachments.cblite");

            manager.ReplaceDatabase("replaced", dbStream, null);
            dbStream.Dispose();

            //Now validate the number of files in the DB
            var db = manager.GetDatabase("replaced");
            Assert.AreEqual(10, db.GetDocumentCount());
            db.Dispose();
        }

        [Test]
        public void TestReplaceDatabaseNamedWithAttachments() {
            var dbStream = GetAsset("withattachments.cblite");
            var attachments = new Dictionary<string, Stream>();
            attachments["356a192b7913b04c54574d18c28d46e6395428ab.blob"] = GetAsset("attachment.blob");
            manager.ReplaceDatabase("replaced", dbStream, attachments);
            dbStream.Dispose();
            //Validate the number of files in the DB
            Assert.AreEqual(1, manager.GetDatabase("replaced").GetDocumentCount());

            var doc = manager.GetDatabase("replaced").GetExistingDocument("168e0c56-4588-4df4-8700-4d5115fa9c74");
            Assert.IsNotNull(doc);
            Assert.IsNotNull(doc.CurrentRevision.Attachments.ElementAt(0));
            Assert.IsNotNull(doc.CurrentRevision.Attachments.ElementAt(0).Content);
        }

        [Test]
        public void TestReplaceWithIosDatabase() {
            if (_storageType == "SQLite") {
                using (var assetStream = GetAsset("ios104.zip")) {
                    manager.ReplaceDatabase("iosdb", assetStream, true);
                }

                var db = manager.GetExistingDatabase("iosdb");
                Assert.IsNotNull(db, "Failed to import database");
                var doc = db.GetExistingDocument("BC38EA44-E153-429A-A698-0CBE6B0090C4");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(doc.CurrentRevision.AttachmentNames.Count(), 2, "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }

                var view = db.GetView("view");
                view.SetMap((d, emit) =>
                {
                    if (d.CblID().Equals("BC38EA44-E153-429A-A698-0CBE6B0090C4")) {
                        emit(d.CblID(), null);
                    }
                }, "1");
                var result = view.CreateQuery().Run();
                Assert.AreEqual(1, result.Count);

                db.Dispose();
                using (var assetStream = GetAsset("ios110.zip")) {
                    manager.ReplaceDatabase("iosdb", assetStream, true);
                }
                db = manager.GetExistingDatabase("iosdb");
                Assert.IsNotNull(db, "Failed to import database");
                doc = db.GetExistingDocument("-iTji_n2zmHpmgYecaRHqZE");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(doc.CurrentRevision.AttachmentNames.Count(), 2, "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }

                view = db.GetView("view");
                view.SetMap((d, emit) =>
                {
                    if (d.CblID().Equals("-iTji_n2zmHpmgYecaRHqZE")) {
                        emit(d.CblID(), null);
                    }
                }, "1");
                result = view.CreateQuery().Run();
                Assert.AreEqual(1, result.Count);

                db.Dispose();
                using (var assetStream = GetAsset("ios120.zip")) {
                    manager.ReplaceDatabase("iosdb", assetStream, true);
                }
                db = manager.GetExistingDatabase("iosdb");
                Assert.IsNotNull(db, "Failed to import database");
                doc = db.GetExistingDocument("doc1");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(2, doc.CurrentRevision.AttachmentNames.Count(), "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }
                    
                view = db.GetView("view");
                view.SetMap((d, emit) =>
                {
                    if (d.CblID().Equals("doc1")) {
                        emit(d.CblID(), null);
                    }
                }, "1");
                result = view.CreateQuery().Run();
                Assert.AreEqual(1, result.Count);
            } else {
                using (var assetStream = GetAsset("ios120-forestdb.zip")) {
                    manager.ReplaceDatabase("iosdb", assetStream, true);
                }

                var db = manager.GetExistingDatabase("iosdb");
                Assert.IsNotNull(db, "Failed to import database");

                var doc = db.GetExistingDocument("doc1");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(1, doc.CurrentRevision.AttachmentNames.Count(), "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }

                doc = db.GetExistingDocument("doc2");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(1, doc.CurrentRevision.AttachmentNames.Count(), "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }

                var view = db.GetView("view");
                view.SetMap((d, emit) =>
                {
                    if (d.CblID().Equals("doc1")) {
                        emit(d.CblID(), null);
                    }
                }, "1");
                var result = view.CreateQuery().Run();
                Assert.AreEqual(1, result.Count);
            }
        }

        [Test]
        public void TestReplaceWithAndroidDatabase() {
            if (_storageType == "SQLite") {
                using (var assetStream = GetAsset("android104.zip")) {
                    manager.ReplaceDatabase("androiddb", assetStream, true);
                }

                var db = manager.GetExistingDatabase("androiddb");
                Assert.IsNotNull(db, "Failed to import database");
                var doc = db.GetExistingDocument("66ac306d-de93-46c8-b60f-946c16ac4a1d");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(doc.CurrentRevision.AttachmentNames.Count(), 1, "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }

                var view = db.GetView("view");
                view.SetMap((d, emit) =>
                {
                    if (d.CblID().Equals("66ac306d-de93-46c8-b60f-946c16ac4a1d")) {
                        emit(d.CblID(), null);
                    }
                }, "1");
                var result = view.CreateQuery().Run();
                Assert.AreEqual(1, result.Count);
                db.Dispose();

                using (var assetStream = GetAsset("android110.zip")) {
                    manager.ReplaceDatabase("androiddb", assetStream, true);
                }

                db = manager.GetExistingDatabase("androiddb");
                Assert.IsNotNull(db, "Failed to import database");
                doc = db.GetExistingDocument("d3e80747-2568-47c8-81e8-a04ba1b5c5d4");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(doc.CurrentRevision.AttachmentNames.Count(), 1, "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }

                view = db.GetView("view");
                view.SetMap((d, emit) =>
                {
                    if (d.CblID().Equals("d3e80747-2568-47c8-81e8-a04ba1b5c5d4")) {
                        emit(d.CblID(), null);
                    }
                }, "1");
                result = view.CreateQuery().Run();
                Assert.AreEqual(1, result.Count);

                db.Dispose();
                using (var assetStream = GetAsset("android120.zip")) {
                    manager.ReplaceDatabase("androiddb", assetStream, true);
                }

                db = manager.GetExistingDatabase("androiddb");
                Assert.IsNotNull(db, "Failed to import database");
                doc = db.GetExistingDocument("doc1");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(doc.CurrentRevision.AttachmentNames.Count(), 1, "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }

                doc = db.GetExistingDocument("doc2");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(doc.CurrentRevision.AttachmentNames.Count(), 1, "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }

                Assert.IsNotNull(db.GetExistingLocalDocument("local1"));

                view = db.GetView("view");
                view.SetMap((d, emit) =>
                {
                    if (d.CblID().Equals("doc1")) {
                        emit(d.CblID(), null);
                    }
                }, "1");
                result = view.CreateQuery().Run();
                Assert.AreEqual(1, result.Count);
            } else {
                using (var assetStream = GetAsset("android120-forestdb.zip")) {
                    manager.ReplaceDatabase("androiddb", assetStream, true);
                }

                var db = manager.GetExistingDatabase("androiddb");
                Assert.IsNotNull(db, "Failed to import database");

                var doc = db.GetExistingDocument("doc1");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(1, doc.CurrentRevision.AttachmentNames.Count(), "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }

                doc = db.GetExistingDocument("doc2");
                Assert.IsNotNull(doc, "Failed to get doc from imported database");
                Assert.AreEqual(1, doc.CurrentRevision.AttachmentNames.Count(), "Failed to get attachments from imported database");
                using (var attachment = doc.CurrentRevision.Attachments.ElementAt(0)) {
                    Assert.IsNotNull(attachment.Content, "Failed to get attachment data");
                }

                var view = db.GetView("view");
                view.SetMap((d, emit) =>
                {
                    if (d.CblID().Equals("doc1")) {
                        emit(d.CblID(), null);
                    }
                }, "1");
                var result = view.CreateQuery().Run();
                Assert.AreEqual(1, result.Count);
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
                using (var assetStream = GetAsset("android104.zip")) {
                    manager.ReplaceDatabase(database.Name, assetStream, false);
                }
            });

            // Verify that the original DB is intact
            doc = database.GetExistingDocument(doc.Id);
            Assert.IsNotNull(doc, "Failed to get original document");
            Assert.AreEqual("bar", doc.UserProperties["foo"]);
        }
    }
}

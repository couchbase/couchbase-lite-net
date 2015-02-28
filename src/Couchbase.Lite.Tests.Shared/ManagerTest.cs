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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Couchbase.Lite;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
    public class ManagerTest : LiteTestCase
    {
        [Test]
        public void TestServer()
        {
            //to ensure this test is easily repeatable we will explicitly remove
            //any stale foo.cblite
            var mustExist = true;
            Database old = manager.GetDatabaseWithoutOpening("foo", mustExist);
            if (old != null)
            {
                old.Delete();
            }

            mustExist = false;
            var db = manager.GetDatabaseWithoutOpening("foo", mustExist);
            Assert.IsNotNull(db);
            Assert.AreEqual("foo", db.Name);
            Assert.IsTrue(db.Path.StartsWith(GetServerPath()));
            Assert.IsFalse(db.Exists());

            // because foo doesn't exist yet
            List<string> databaseNames = manager.AllDatabaseNames.ToList();

            Assert.IsTrue(!databaseNames.Contains("foo"));
            Assert.IsTrue(db.Open());
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
            var testDirName = "test-directory-" + Runtime.CurrentTimeMillis();
            var rootDirPath = GetRootDirectory().FullName;
            var testDirPath = Path.Combine(rootDirPath, testDirName);
            var testDirInfo = Directory.CreateDirectory(testDirPath);

            var oldTouchDb = Path.Combine(testDirPath, "old" + Manager.DatabaseSuffixOld);
            File.Create(oldTouchDb);

            var newCbLiteDb = Path.Combine(testDirPath, "new" + Manager.DatabaseSuffix);
            File.Create(newCbLiteDb);

            var migratedOldFile = Path.Combine(testDirPath, "old" + Manager.DatabaseSuffix);
            File.Create(migratedOldFile);

            StopCBLite();
            manager = new Manager(testDirInfo, Manager.DefaultOptions);

            var oldTouchDbInfo = new FileInfo(oldTouchDb);
            var newCbLiteDbInfo = new FileInfo(newCbLiteDb);
            var migratedOldInfo = new FileInfo(migratedOldFile);

            Assert.IsTrue(migratedOldInfo.Exists);
            //cannot rename old.touchdb in old.cblite, old.cblite already exists
            Assert.IsTrue(oldTouchDbInfo.Exists);
            Assert.IsTrue(newCbLiteDbInfo.Exists);
            Assert.AreEqual(3, testDirInfo.GetFiles().Length);

            StopCBLite();
            migratedOldInfo.Delete();
            manager = new Manager(testDirInfo, Manager.DefaultOptions);

            oldTouchDbInfo = new FileInfo(oldTouchDb);
            newCbLiteDbInfo = new FileInfo(newCbLiteDb);
            migratedOldInfo = new FileInfo(migratedOldFile);

            //rename old.touchdb in old.cblite, previous old.cblite already doesn't exist
            Assert.IsTrue(migratedOldInfo.Exists);
            Assert.IsFalse(oldTouchDbInfo.Exists);
            Assert.IsTrue(newCbLiteDbInfo.Exists);    
            Assert.AreEqual(2, testDirInfo.GetFiles().Length); 
        }

        [Test]
        public void TestReplaceDatabaseNamedNoAttachments() {
            //Copy database from assets to local storage
            var dbStream = GetAsset("noattachments.cblite");

            manager.ReplaceDatabase("replaced", dbStream, null);
            dbStream.Dispose();

            //Now validate the number of files in the DB
            Assert.AreEqual(10, manager.GetDatabase("replaced").DocumentCount);
        }

        [Test]
        public void TestReplaceDatabaseNamedWithAttachments() {
            var dbStream = GetAsset("withattachments.cblite");
            var attachments = new Dictionary<string, Stream>();
            attachments["356a192b7913b04c54574d18c28d46e6395428ab.blob"] = GetAsset("attachment.blob");
            manager.ReplaceDatabase("replaced", dbStream, attachments);
            dbStream.Dispose();
            //Validate the number of files in the DB
            Assert.AreEqual(1, manager.GetDatabase("replaced").DocumentCount);

            var doc = manager.GetDatabase("replaced").GetExistingDocument("168e0c56-4588-4df4-8700-4d5115fa9c74");
            Assert.IsNotNull(doc);
            Assert.IsNotNull(doc.CurrentRevision.Attachments.ElementAt(0));
            Assert.IsNotNull(doc.CurrentRevision.Attachments.ElementAt(0).Content);
        }
    }
}

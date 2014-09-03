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
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite
{
    public class ManagerTest : LiteTestCase
    {
        public const string Tag = "ManagerTest";

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestServer()
        {
            //to ensure this test is easily repeatable we will explicitly remove
            //any stale foo.cblite
            bool mustExist = true;
            Database old = manager.GetDatabaseWithoutOpening("foo", mustExist);
            if (old != null)
            {
                old.Delete();
            }
            mustExist = false;
            Database db = manager.GetDatabaseWithoutOpening("foo", mustExist);
            NUnit.Framework.Assert.IsNotNull(db);
            NUnit.Framework.Assert.AreEqual("foo", db.GetName());
            NUnit.Framework.Assert.IsTrue(db.GetPath().StartsWith(new LiteTestContext().GetRootDirectory
                ().GetAbsolutePath()));
            NUnit.Framework.Assert.IsFalse(db.Exists());
            // because foo doesn't exist yet
            IList<string> databaseNames = manager.GetAllDatabaseNames();
            NUnit.Framework.Assert.IsTrue(!databaseNames.Contains("foo"));
            NUnit.Framework.Assert.IsTrue(db.Open());
            NUnit.Framework.Assert.IsTrue(db.Exists());
            databaseNames = manager.GetAllDatabaseNames();
            NUnit.Framework.Assert.IsTrue(databaseNames.Contains("foo"));
            db.Close();
            db.Delete();
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void TestUpgradeOldDatabaseFiles()
        {
            string directoryName = "test-directory-" + Runtime.CurrentTimeMillis();
            LiteTestContext context = new LiteTestContext(directoryName);
            FilePath directory = context.GetFilesDir();
            if (!directory.Exists())
            {
                bool result = directory.Mkdir();
                if (!result)
                {
                    throw new IOException("Unable to create directory " + directory);
                }
            }
            FilePath oldTouchDbFile = new FilePath(directory, string.Format("old%s", Manager.
                DatabaseSuffixOld));
            oldTouchDbFile.CreateNewFile();
            FilePath newCbLiteFile = new FilePath(directory, string.Format("new%s", Manager.DatabaseSuffix
                ));
            newCbLiteFile.CreateNewFile();
            FilePath migratedOldFile = new FilePath(directory, string.Format("old%s", Manager
                .DatabaseSuffix));
            migratedOldFile.CreateNewFile();
            base.StopCBLite();
            manager = new Manager(context, Manager.DefaultOptions);
            NUnit.Framework.Assert.IsTrue(migratedOldFile.Exists());
            //cannot rename old.touchdb to old.cblite, because old.cblite already exists
            NUnit.Framework.Assert.IsTrue(oldTouchDbFile.Exists());
            NUnit.Framework.Assert.IsTrue(newCbLiteFile.Exists());
            NUnit.Framework.Assert.AreEqual(3, directory.ListFiles().Length);
            base.StopCBLite();
            migratedOldFile.Delete();
            manager = new Manager(context, Manager.DefaultOptions);
            //rename old.touchdb in old.cblite, previous old.cblite already doesn't exist
            NUnit.Framework.Assert.IsTrue(migratedOldFile.Exists());
            NUnit.Framework.Assert.IsTrue(oldTouchDbFile.Exists() == false);
            NUnit.Framework.Assert.IsTrue(newCbLiteFile.Exists());
            NUnit.Framework.Assert.AreEqual(2, directory.ListFiles().Length);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestReplaceDatabaseNamedNoAttachments()
        {
            //Copy database from assets to local storage
            InputStream dbStream = GetAsset("noattachments.cblite");
            manager.ReplaceDatabase("replaced", dbStream, null);
            //Now validate the number of files in the DB
            NUnit.Framework.Assert.AreEqual(10, manager.GetDatabase("replaced").GetDocumentCount
                ());
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestReplaceDatabaseNamedWithAttachments()
        {
            InputStream dbStream = GetAsset("withattachments.cblite");
            string[] attachmentlist = null;
            IDictionary<string, InputStream> attachments = new Dictionary<string, InputStream
                >();
            InputStream blobStream = GetAsset("attachments/356a192b7913b04c54574d18c28d46e6395428ab.blob"
                );
            attachments.Put("356a192b7913b04c54574d18c28d46e6395428ab.blob", blobStream);
            manager.ReplaceDatabase("replaced2", dbStream, attachments);
            //Validate the number of files in the DB
            NUnit.Framework.Assert.AreEqual(1, manager.GetDatabase("replaced2").GetDocumentCount
                ());
            //get the attachment from the document
            Document doc = manager.GetDatabase("replaced2").GetExistingDocument("168e0c56-4588-4df4-8700-4d5115fa9c74"
                );
            NUnit.Framework.Assert.IsNotNull(doc);
            RevisionInternal gotRev1 = database.GetDocumentWithIDAndRev(doc.GetId(), doc.GetCurrentRevisionId
                (), EnumSet.NoneOf<Database.TDContentOptions>());
        }
    }
}

//
// LiteTestCase.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;

using System.Net;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;
using Couchbase.Lite.Tests;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using System.IO.Compression;

namespace Couchbase.Lite
{
    [TestFixture]
    public abstract class LiteTestCase
    {
        private const string Tag = "LiteTestCase";

        public const string FacebookAppId = "78255794086";

        ObjectWriter mapper = new ObjectWriter();

        protected Manager manager = null;

        protected Database database = null;

        protected string DefaultTestDb = "cblitetest";

        private static DirectoryInfo _rootDir;
        public static DirectoryInfo RootDirectory { 
            get {
                if (_rootDir == null) {
                    var path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    _rootDir = new DirectoryInfo(Path.Combine(path, Path.Combine("couchbase", Path.Combine("tests", "files"))));
                }

                return _rootDir;
            }
            set { 
                var path = value.FullName;
                _rootDir = new DirectoryInfo(Path.Combine(path, Path.Combine("couchbase", Path.Combine("tests", "files"))));;
            }
        }


        [SetUp]
        protected virtual void SetUp()
        {
            Log.V(Tag, "SetUp");
            ManagerOptions.Default.CallbackScheduler = new SingleTaskThreadpoolScheduler();

            LoadCustomProperties();
            StartCBLite();
            StartDatabase();
        }

        protected Stream GetAsset(string name)
        {
            var assetPath = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".Assets." + name;
            Log.D(Tag, "Fetching assembly resource: " + assetPath);
            var stream = GetType().GetResourceAsStream(assetPath);
            return stream;
        }

        protected string GetServerPath()
        {
            var filesDir = RootDirectory.FullName;
            return filesDir;
        }

        /// <exception cref="System.IO.IOException"></exception>
        protected void StartCBLite()
        {
            string serverPath = GetServerPath();
            var path = new DirectoryInfo(serverPath);

            if (path.Exists)
                path.Delete(true);

            path.Create();

            var testPath = path.CreateSubdirectory("tests");
            manager = new Manager(testPath, Manager.DefaultOptions);
        }

        protected void StopCBLite()
        {
            if (manager != null)
            {
                manager.Close();
            }
        }

        protected Database StartDatabase()
        {
            if (database != null)
            {
                database.Close();
                database.Delete();
                database = null;
            }
            database = EnsureEmptyDatabase(DefaultTestDb);
            return database;
        }

        protected void StopDatabase()
        {
            if (database != null)
            {
                database.Close();
            }
        }

        protected Database EnsureEmptyDatabase(string dbName)
        {
            var db = manager.GetExistingDatabase(dbName);
            if (db != null)
            {
                var status = false;;

                try {
                    db.Delete();
                    db.Close();
                    status = true;
                } catch (Exception e) { 
                    Log.E(Tag, "Cannot delete database " + e.Message);
                }

                Assert.IsTrue(status);
            }
            db = manager.GetDatabase(dbName);
            return db;
        }

        /// <exception cref="System.IO.IOException"></exception>
        protected void LoadCustomProperties()
        {
            var systemProperties = Runtime.Properties;
            InputStream mainProperties = GetAsset("test.properties");
            if (mainProperties != null)
            {
                systemProperties.Load(mainProperties);
            }
            try
            {
                var localProperties = GetAsset("local-test.properties");
                if (localProperties != null)
                {
                    systemProperties.Load(localProperties);
                }
            }
            catch (IOException)
            {
                Log.W(Tag, "Error trying to read from local-test.properties, does this file exist?");
            }
        }

        protected string GetReplicationProtocol()
        {
            return Runtime.GetProperty("replicationProtocol");
        }

        protected string GetReplicationServer()
        {
            return Runtime.GetProperty("replicationServer").Trim();
        }

        protected int GetReplicationPort()
        {
            return Convert.ToInt32(Runtime.GetProperty("replicationPort"));
        }

        protected int GetReplicationAdminPort()
        {
            return Convert.ToInt32(Runtime.GetProperty("replicationAdminPort"));
        }

        protected string GetReplicationAdminUser()
        {
            return Runtime.GetProperty("replicationAdminUser");
        }

        protected string GetReplicationAdminPassword()
        {
            return Runtime.GetProperty("replicationAdminPassword");
        }

        protected string GetReplicationDatabase()
        {
            return Runtime.GetProperty("replicationDatabase");
        }

        protected Uri GetReplicationURL()
        {
            String path = null;
            try
            {
                if (GetReplicationAdminUser() != null && GetReplicationAdminUser().Trim().Length > 0)
                {
                    path = string.Format("{0}://{1}:{2}@{3}:{4}/{5}", GetReplicationProtocol(), GetReplicationAdminUser
                        (), GetReplicationAdminPassword(), GetReplicationServer(), GetReplicationPort(), 
                        GetReplicationDatabase());
                    return new Uri(path);
                }
                else
                {
                    path = string.Format("{0}://{1}:{2}/{3}", GetReplicationProtocol(), GetReplicationServer
                        (), GetReplicationPort(), GetReplicationDatabase());
                    return new Uri(path);
                }
            }
            catch (UriFormatException e)
            {
                throw new ArgumentException(String.Format("Invalid replication URL: {0}", path), e);
            }
        }

        protected bool IsTestingAgainstSyncGateway()
        {
            return GetReplicationPort() == 4984;
        }

        protected void AssertDictionariesAreEqual(IDictionary<string, object> first, IDictionary<string, object> second)
        {
            //I'm tired of NUnit misunderstanding that objects are dictionaries and trying to compare them as collections...
            Assert.IsTrue(first.Keys.Count == second.Keys.Count);
            foreach (var key in first.Keys) {
                var firstObj = first[key];
                var secondObj = second[key];
                var firstDic = firstObj.AsDictionary<string, object>();
                var secondDic = secondObj.AsDictionary<string, object>();
                if (firstDic != null && secondDic != null) {
                    AssertDictionariesAreEqual(firstDic, secondDic);
                } else {
                    Assert.AreEqual(firstObj, secondObj);
                }
            }
        }

        /// <exception cref="System.UriFormatException"></exception>
        protected Uri GetReplicationURLWithoutCredentials()
        {
            return new Uri(string.Format("{0}://{1}:{2}/{3}", GetReplicationProtocol(), GetReplicationServer(), GetReplicationPort(), GetReplicationDatabase()));
        }

        protected Uri GetReplicationAdminURL()
        {
            return new Uri(string.Format("{0}://{1}:{2}/{3}", GetReplicationProtocol(), GetReplicationServer(), GetReplicationAdminPort(), GetReplicationDatabase()));
        }

        [TearDown]
        protected virtual void TearDown()
        {
            Log.V(Tag, "tearDown");
            StopDatabase();
            StopCBLite();
            Manager.DefaultOptions.RestoreDefaults();
        }

        protected virtual void RunReplication(Replication replication)
        {
            var replicationDoneSignal = new CountdownEvent(1);
            var observer = new ReplicationObserver(replicationDoneSignal);
            replication.Changed += observer.Changed;
            replication.Start();
            var success = replicationDoneSignal.Wait(TimeSpan.FromSeconds(60));
            Assert.IsTrue(success);

            replication.Changed -= observer.Changed;
        }

        protected IDictionary<string, object> UserProperties(IDictionary
            <string, object> properties)
        {
            var result = new Dictionary<string, object>();
            foreach (string key in properties.Keys)
            {
                if (!key.StartsWith ("_", StringComparison.Ordinal))
                {
                    result.Put(key, properties[key]);
                }
            }
            return result;
        }

        protected IDictionary<string, object> CreateAttachmentsStub(string name)
        {
            return new Dictionary<string, object> {
                { name, new Dictionary<string, object> {
                        { "stub", true }
                    }
                }
            };
        }

        protected IDictionary<string, object> CreateAttachmentsDict(IEnumerable<byte> data, string name, string type, bool gzipped)
        {
            if (gzipped) {
                using (var ms = new MemoryStream())
                using (var gs = new GZipStream(ms, CompressionMode.Compress)) {
                    gs.Write(data.ToArray(), 0, data.Count());
                    data = ms.ToArray();
                }
            }

            var att = new NonNullDictionary<string, object> {
                { "content_type", type },
                { "data", data },
                { "encoding", gzipped ? "gzip" : null }
            };

            return new Dictionary<string, object> {
                { name, att }
            };
        }

        /// <exception cref="System.IO.IOException"></exception>
        public virtual IDictionary<string, object> GetReplicationAuthParsedJson()
        {
            var authJson = "{\n" + "    \"facebook\" : {\n" + "        \"email\" : \"jchris@couchbase.com\"\n"
                + "     }\n" + "   }\n";
            mapper = new ObjectWriter();
            var authProperties = mapper.ReadValue<Dictionary<string, object>>(authJson);
            return authProperties;
        }

        /// <exception cref="System.IO.IOException"></exception>
        public virtual IDictionary<string, object> GetPushReplicationParsedJson()
        {
            IDictionary<string, object> authProperties = GetReplicationAuthParsedJson();
            IDictionary<string, object> targetProperties = new Dictionary<string, object>();
            targetProperties.Put("url", GetReplicationURL().ToString());
            targetProperties["auth"] = authProperties;
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties["source"] = DefaultTestDb;
            properties["target"] = targetProperties;
            return properties;
        }

        /// <exception cref="System.IO.IOException"></exception>
        public virtual IDictionary<string, object> GetPullReplicationParsedJson()
        {
            IDictionary<string, object> authProperties = GetReplicationAuthParsedJson();
            IDictionary<string, object> sourceProperties = new Dictionary<string, object>();
            sourceProperties.Put("url", GetReplicationURL().ToString());
            sourceProperties["auth"] = authProperties;
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties["source"] = sourceProperties;
            properties["target"] = DefaultTestDb;
            return properties;
        }

        internal static void CreateDocuments(Database db, int n)
        {
            for (int i = 0; i < n; i++) {
                var properties = new Dictionary<string, object>();
                properties.Add("testName", "testDatabase");
                properties.Add("sequence", i);
                CreateDocumentWithProperties(db, properties);
            }
        }

        internal static Task CreateDocumentsAsync(Database database, int n)
        {
            return database.RunAsync(db => 
            {
                database.RunInTransaction(() => 
                {
                    LiteTestCase.CreateDocuments(db, n);
                    return true;
                });
            });
        }

        internal static Document CreateDocumentWithProperties(Database db, IDictionary<string, object> properties) 
        {
            var doc = db.CreateDocument();

            Assert.IsNotNull(doc);
            Assert.IsNull(doc.CurrentRevisionId);
            Assert.IsNull(doc.CurrentRevision);
            Assert.IsNotNull(doc.Id, "Document has no ID");

            try
            {
                doc.PutProperties(properties);
            } 
            catch (Exception e)
            {
                Log.E(Tag, "Error creating document", e);
                Assert.IsTrue(false, "can't create new document in db:" + db.Name + " with properties:" + properties.ToString());
            }

            Assert.IsNotNull(doc.Id);
            Assert.IsNotNull(doc.CurrentRevisionId);
            Assert.IsNotNull(doc.CurrentRevision);

            // should be same doc instance, since there should only ever be a single Document instance for a given document
            Assert.AreEqual(db.GetDocument(doc.Id), doc);
            Assert.AreEqual(db.GetDocument(doc.Id).Id, doc.Id);

            return doc;
        }

        internal static Document CreateDocWithAttachment(Database database, string attachmentName, string content)
        {
            var properties = new Dictionary<string, object>();
            properties.Put("foo", "bar");

            var doc = CreateDocumentWithProperties(database, properties);
            var rev = doc.CurrentRevision;
            var attachment = rev.GetAttachment(attachmentName);
            Assert.AreEqual(rev.Attachments.Count(), 0);
            Assert.AreEqual(rev.AttachmentNames.Count(), 0);
            Assert.IsNull(attachment);

            var body = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var rev2 = doc.CreateRevision();
            rev2.SetAttachment(attachmentName, "text/plain; charset=utf-8", body);
            var rev3 = rev2.Save();
            rev2.Dispose();
            Assert.IsNotNull(rev3);
            Assert.AreEqual(rev3.Attachments.Count(), 1);
            Assert.AreEqual(rev3.AttachmentNames.Count(), 1);

            attachment = rev3.GetAttachment(attachmentName);
            Assert.IsNotNull(attachment);
            Assert.AreEqual(doc, attachment.Document);
            Assert.AreEqual(attachmentName, attachment.Name);

            var attNames = new List<string>();
            attNames.AddItem(attachmentName);
            Assert.AreEqual(rev3.AttachmentNames, attNames);
            Assert.AreEqual("text/plain; charset=utf-8", attachment.ContentType);
            Assert.AreEqual(Encoding.UTF8.GetString(attachment.Content.ToArray()), content);
            Assert.AreEqual(Encoding.UTF8.GetBytes(content).Length, attachment.Length);
            attachment.Dispose();

            return doc;
        }          

        public void StopReplication(Replication replication)
        {
            if (replication.Status == ReplicationStatus.Stopped) {
                return;
            }

            var replicationDoneSignal = new CountdownEvent(1);
            var replicationStoppedObserver = new ReplicationObserver(replicationDoneSignal);
            replication.Changed += replicationStoppedObserver.Changed;
            replication.Stop();

            var success = replicationDoneSignal.Wait(TimeSpan.FromSeconds(10));
            Assert.IsTrue(success);

            // give a little padding to give it a chance to save a checkpoint
            Thread.Sleep(2 * 1000);
        }

        protected void AssertEnumerablesAreEqual(
            IEnumerable list1, 
            IEnumerable list2)
        {
            var enumerator1 = list1.GetEnumerator();
            var enumerator2 = list2.GetEnumerator();

            while (enumerator1.MoveNext() && enumerator2.MoveNext())
            {
                var obj1 = enumerator1.Current;
                var obj2 = enumerator1.Current;

                if (obj1 is IDictionary<string, object> && obj2 is IDictionary<string, object>) 
                {
                    AssertPropertiesAreEqual((IDictionary<string, object>)obj1, (IDictionary<string, object>)obj2);
                }
                else if (obj1 is IEnumerable && obj2 is IEnumerable)
                {
                    AssertEnumerablesAreEqual((IEnumerable)obj1, (IEnumerable)obj2);
                }
                else
                {
                    Assert.AreEqual(obj1, obj2);
                }
            }
        }

        protected void AssertPropertiesAreEqual(
            IDictionary<string, object> prop1,
            IDictionary<string, object> prop2)
        {
            Assert.AreEqual(prop1.Count, prop2.Count);
            foreach(var key in prop1.Keys) 
            {
                Assert.IsTrue(prop1.ContainsKey(key));
                object obj1 = prop1[key];
                object obj2 = prop2[key];

                if (obj1 is IDictionary && obj2 is IDictionary) 
                {
                    AssertPropertiesAreEqual((IDictionary<string, object>)obj1, (IDictionary<string, object>)obj2);
                }
                else if (obj1 is IEnumerable && obj2 is IEnumerable)
                {
                    AssertEnumerablesAreEqual((IEnumerable)obj1, (IEnumerable)obj2);
                }
                else
                {
                    Assert.AreEqual(obj1, obj2);
                }
            }
        }

        /// <exception cref="System.Exception"></exception>
        public static SavedRevision CreateRevisionWithRandomProps(SavedRevision createRevFrom, bool allowConflict)
        {
            var properties = new Dictionary<string, object>();
            properties.Put(Misc.CreateGUID(), "val");

            var unsavedRevision = createRevFrom.CreateRevision();
            unsavedRevision.SetUserProperties(properties);

            return unsavedRevision.Save(allowConflict);
        }
    }

    internal class ReplicationStoppedObserver
    {
        private readonly CountDownLatch doneSignal;

        public ReplicationStoppedObserver(CountDownLatch doneSignal)
        {
            this.doneSignal = doneSignal;
        }

        public void Changed(ReplicationChangeEventArgs args)
        {
            var replicator = args.Source;
            if (replicator.Status == ReplicationStatus.Stopped)
            {
                doneSignal.CountDown();
            }
        }
    }

    internal class ReplicationErrorObserver
    {
        private readonly CountDownLatch doneSignal;

        public ReplicationErrorObserver(CountDownLatch doneSignal)
        {
            this.doneSignal = doneSignal;
        }

        public void Changed(ReplicationChangeEventArgs args)
        {
            var replicator = args.Source;
            if (replicator.LastError != null)
            {
                doneSignal.CountDown();
            }
        }
    }
}
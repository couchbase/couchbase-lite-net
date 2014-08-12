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
//using System;
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Router;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Org.Apache.Commons.IO;
using Sharpen;

namespace Couchbase.Lite
{
    public abstract class LiteTestCase : TestCase
    {
        public const string Tag = "LiteTestCase";

        private static bool initializedUrlHandler = false;

        protected internal ObjectWriter mapper = new ObjectWriter();

        protected internal Manager manager = null;

        protected internal Database database = null;

        protected internal string DefaultTestDb = "cblite-test";

        /// <exception cref="System.Exception"></exception>
        protected override void SetUp()
        {
            Log.V(Tag, "setUp");
            base.SetUp();
            //for some reason a traditional static initializer causes junit to die
            if (!initializedUrlHandler)
            {
                URLStreamHandlerFactory.RegisterSelfIgnoreError();
                initializedUrlHandler = true;
            }
            LoadCustomProperties();
            StartCBLite();
            StartDatabase();
        }

        protected internal virtual InputStream GetAsset(string name)
        {
            return this.GetType().GetResourceAsStream("/assets/" + name);
        }

        /// <exception cref="System.IO.IOException"></exception>
        protected internal virtual void StartCBLite()
        {
            LiteTestContext context = new LiteTestContext();
            string serverPath = context.GetRootDirectory().GetAbsolutePath();
            FilePath serverPathFile = new FilePath(serverPath);
            FileDirUtils.DeleteRecursive(serverPathFile);
            serverPathFile.Mkdir();
            Manager.EnableLogging(Log.Tag, Log.Verbose);
            Manager.EnableLogging(Log.TagSync, Log.Verbose);
            Manager.EnableLogging(Log.TagQuery, Log.Verbose);
            Manager.EnableLogging(Log.TagView, Log.Verbose);
            Manager.EnableLogging(Log.TagChangeTracker, Log.Verbose);
            Manager.EnableLogging(Log.TagBlobStore, Log.Verbose);
            Manager.EnableLogging(Log.TagDatabase, Log.Verbose);
            Manager.EnableLogging(Log.TagListener, Log.Verbose);
            Manager.EnableLogging(Log.TagMultiStreamWriter, Log.Verbose);
            Manager.EnableLogging(Log.TagRemoteRequest, Log.Verbose);
            Manager.EnableLogging(Log.TagRouter, Log.Verbose);
            manager = new Manager(context, Manager.DefaultOptions);
        }

        protected internal virtual void StopCBLite()
        {
            if (manager != null)
            {
                manager.Close();
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        protected internal virtual Database StartDatabase()
        {
            database = EnsureEmptyDatabase(DefaultTestDb);
            return database;
        }

        protected internal virtual void StopDatabse()
        {
            if (database != null)
            {
                database.Close();
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        protected internal virtual Database EnsureEmptyDatabase(string dbName)
        {
            Database db = manager.GetExistingDatabase(dbName);
            if (db != null)
            {
                db.Delete();
            }
            db = manager.GetDatabase(dbName);
            return db;
        }

        /// <exception cref="System.IO.IOException"></exception>
        protected internal virtual void LoadCustomProperties()
        {
            Properties systemProperties = Runtime.GetProperties();
            InputStream mainProperties = GetAsset("test.properties");
            if (mainProperties != null)
            {
                systemProperties.Load(mainProperties);
            }
            try
            {
                InputStream localProperties = GetAsset("local-test.properties");
                if (localProperties != null)
                {
                    systemProperties.Load(localProperties);
                }
            }
            catch (IOException)
            {
                Log.W(Tag, "Error trying to read from local-test.properties, does this file exist?"
                    );
            }
        }

        protected internal virtual string GetReplicationProtocol()
        {
            return Runtime.GetProperty("replicationProtocol");
        }

        protected internal virtual string GetReplicationServer()
        {
            return Runtime.GetProperty("replicationServer");
        }

        protected internal virtual int GetReplicationPort()
        {
            return System.Convert.ToInt32(Runtime.GetProperty("replicationPort"));
        }

        protected internal virtual string GetReplicationAdminUser()
        {
            return Runtime.GetProperty("replicationAdminUser");
        }

        protected internal virtual string GetReplicationAdminPassword()
        {
            return Runtime.GetProperty("replicationAdminPassword");
        }

        protected internal virtual string GetReplicationDatabase()
        {
            return Runtime.GetProperty("replicationDatabase");
        }

        protected internal virtual Uri GetReplicationURL()
        {
            try
            {
                if (GetReplicationAdminUser() != null && GetReplicationAdminUser().Trim().Length 
                    > 0)
                {
                    return new Uri(string.Format("%s://%s:%s@%s:%d/%s", GetReplicationProtocol(), GetReplicationAdminUser
                        (), GetReplicationAdminPassword(), GetReplicationServer(), GetReplicationPort(), 
                        GetReplicationDatabase()));
                }
                else
                {
                    return new Uri(string.Format("%s://%s:%d/%s", GetReplicationProtocol(), GetReplicationServer
                        (), GetReplicationPort(), GetReplicationDatabase()));
                }
            }
            catch (UriFormatException e)
            {
                throw new ArgumentException(e);
            }
        }

        protected internal virtual bool IsTestingAgainstSyncGateway()
        {
            return GetReplicationPort() == 4984;
        }

        /// <exception cref="System.UriFormatException"></exception>
        protected internal virtual Uri GetReplicationURLWithoutCredentials()
        {
            return new Uri(string.Format("%s://%s:%d/%s", GetReplicationProtocol(), GetReplicationServer
                (), GetReplicationPort(), GetReplicationDatabase()));
        }

        /// <exception cref="System.Exception"></exception>
        protected override void TearDown()
        {
            Log.V(Tag, "tearDown");
            base.TearDown();
            StopDatabse();
            StopCBLite();
        }

        protected internal virtual IDictionary<string, object> UserProperties(IDictionary
            <string, object> properties)
        {
            IDictionary<string, object> result = new Dictionary<string, object>();
            foreach (string key in properties.Keys)
            {
                if (!key.StartsWith("_"))
                {
                    result.Put(key, properties.Get(key));
                }
            }
            return result;
        }

        /// <exception cref="System.IO.IOException"></exception>
        public virtual IDictionary<string, object> GetReplicationAuthParsedJson()
        {
            string authJson = "{\n" + "    \"facebook\" : {\n" + "        \"email\" : \"jchris@couchbase.com\"\n"
                 + "     }\n" + "   }\n";
            ObjectWriter mapper = new ObjectWriter();
            IDictionary<string, object> authProperties = mapper.ReadValue(authJson, new _TypeReference_203
                ());
            return authProperties;
        }

        private sealed class _TypeReference_203 : TypeReference<Dictionary<string, object
            >>
        {
            public _TypeReference_203()
            {
            }
        }

        /// <exception cref="System.IO.IOException"></exception>
        public virtual IDictionary<string, object> GetPushReplicationParsedJson()
        {
            IDictionary<string, object> authProperties = GetReplicationAuthParsedJson();
            IDictionary<string, object> targetProperties = new Dictionary<string, object>();
            targetProperties.Put("url", GetReplicationURL().ToExternalForm());
            targetProperties.Put("auth", authProperties);
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Put("source", DefaultTestDb);
            properties.Put("target", targetProperties);
            return properties;
        }

        /// <exception cref="System.IO.IOException"></exception>
        public virtual IDictionary<string, object> GetPullReplicationParsedJson()
        {
            IDictionary<string, object> authProperties = GetReplicationAuthParsedJson();
            IDictionary<string, object> sourceProperties = new Dictionary<string, object>();
            sourceProperties.Put("url", GetReplicationURL().ToExternalForm());
            sourceProperties.Put("auth", authProperties);
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Put("source", sourceProperties);
            properties.Put("target", DefaultTestDb);
            return properties;
        }

        protected internal virtual URLConnection SendRequest(string method, string path, 
            IDictionary<string, string> headers, object bodyObj)
        {
            try
            {
                Uri url = new Uri("cblite://" + path);
                URLConnection conn = (URLConnection)url.OpenConnection();
                conn.SetDoOutput(true);
                conn.SetRequestMethod(method);
                if (headers != null)
                {
                    foreach (string header in headers.Keys)
                    {
                        conn.SetRequestProperty(header, headers.Get(header));
                    }
                }
                IDictionary<string, IList<string>> allProperties = conn.GetRequestProperties();
                if (bodyObj != null)
                {
                    conn.SetDoInput(true);
                    ByteArrayInputStream bais = new ByteArrayInputStream(mapper.WriteValueAsBytes(bodyObj
                        ));
                    conn.SetRequestInputStream(bais);
                }
                Couchbase.Lite.Router.Router router = new Couchbase.Lite.Router.Router(manager, conn
                    );
                router.Start();
                return conn;
            }
            catch (UriFormatException)
            {
                Fail();
            }
            catch (IOException)
            {
                Fail();
            }
            return null;
        }

        protected internal virtual object ParseJSONResponse(URLConnection conn)
        {
            object result = null;
            Body responseBody = conn.GetResponseBody();
            if (responseBody != null)
            {
                byte[] json = responseBody.GetJson();
                string jsonString = null;
                if (json != null)
                {
                    jsonString = Sharpen.Runtime.GetStringForBytes(json);
                    try
                    {
                        result = mapper.ReadValue<object>(jsonString);
                    }
                    catch (Exception)
                    {
                        Fail();
                    }
                }
            }
            return result;
        }

        protected internal virtual object SendBody(string method, string path, object bodyObj
            , int expectedStatus, object expectedResult)
        {
            URLConnection conn = SendRequest(method, path, null, bodyObj);
            object result = ParseJSONResponse(conn);
            Log.V(Tag, string.Format("%s %s --> %d", method, path, conn.GetResponseCode()));
            NUnit.Framework.Assert.AreEqual(expectedStatus, conn.GetResponseCode());
            if (expectedResult != null)
            {
                NUnit.Framework.Assert.AreEqual(expectedResult, result);
            }
            return result;
        }

        protected internal virtual object Send(string method, string path, int expectedStatus
            , object expectedResult)
        {
            return SendBody(method, path, null, expectedStatus, expectedResult);
        }

        public static void CreateDocuments(Database db, int n)
        {
            //TODO should be changed to use db.runInTransaction
            for (int i = 0; i < n; i++)
            {
                IDictionary<string, object> properties = new Dictionary<string, object>();
                properties.Put("testName", "testDatabase");
                properties.Put("sequence", i);
                CreateDocumentWithProperties(db, properties);
            }
        }

        protected internal static Future CreateDocumentsAsync(Database db, int n)
        {
            return db.RunAsync(new _AsyncTask_310(db, n));
        }

        private sealed class _AsyncTask_310 : AsyncTask
        {
            public _AsyncTask_310(Database db, int n)
            {
                this.db = db;
                this.n = n;
            }

            public void Run(Database database)
            {
                db.BeginTransaction();
                LiteTestCase.CreateDocuments(db, n);
                db.EndTransaction(true);
            }

            private readonly Database db;

            private readonly int n;
        }

        public static Document CreateDocumentWithProperties(Database db, IDictionary<string
            , object> properties)
        {
            Document doc = db.CreateDocument();
            NUnit.Framework.Assert.IsNotNull(doc);
            NUnit.Framework.Assert.IsNull(doc.GetCurrentRevisionId());
            NUnit.Framework.Assert.IsNull(doc.GetCurrentRevision());
            NUnit.Framework.Assert.IsNotNull("Document has no ID", doc.GetId());
            // 'untitled' docs are no longer untitled (8/10/12)
            try
            {
                doc.PutProperties(properties);
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error creating document", e);
                NUnit.Framework.Assert.IsTrue("can't create new document in db:" + db.GetName() +
                     " with properties:" + properties.ToString(), false);
            }
            NUnit.Framework.Assert.IsNotNull(doc.GetId());
            NUnit.Framework.Assert.IsNotNull(doc.GetCurrentRevisionId());
            NUnit.Framework.Assert.IsNotNull(doc.GetUserProperties());
            // should be same doc instance, since there should only ever be a single Document instance for a given document
            NUnit.Framework.Assert.AreEqual(db.GetDocument(doc.GetId()), doc);
            NUnit.Framework.Assert.AreEqual(db.GetDocument(doc.GetId()).GetId(), doc.GetId());
            return doc;
        }

        /// <exception cref="System.Exception"></exception>
        public static Document CreateDocWithAttachment(Database database, string attachmentName
            , string content)
        {
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Put("foo", "bar");
            Document doc = CreateDocumentWithProperties(database, properties);
            SavedRevision rev = doc.GetCurrentRevision();
            NUnit.Framework.Assert.AreEqual(rev.GetAttachments().Count, 0);
            NUnit.Framework.Assert.AreEqual(rev.GetAttachmentNames().Count, 0);
            NUnit.Framework.Assert.IsNull(rev.GetAttachment(attachmentName));
            ByteArrayInputStream body = new ByteArrayInputStream(Sharpen.Runtime.GetBytesForString
                (content));
            UnsavedRevision rev2 = doc.CreateRevision();
            rev2.SetAttachment(attachmentName, "text/plain; charset=utf-8", body);
            SavedRevision rev3 = rev2.Save();
            NUnit.Framework.Assert.IsNotNull(rev3);
            NUnit.Framework.Assert.AreEqual(rev3.GetAttachments().Count, 1);
            NUnit.Framework.Assert.AreEqual(rev3.GetAttachmentNames().Count, 1);
            Attachment attach = rev3.GetAttachment(attachmentName);
            NUnit.Framework.Assert.IsNotNull(attach);
            NUnit.Framework.Assert.AreEqual(doc, attach.GetDocument());
            NUnit.Framework.Assert.AreEqual(attachmentName, attach.GetName());
            IList<string> attNames = new AList<string>();
            attNames.AddItem(attachmentName);
            NUnit.Framework.Assert.AreEqual(rev3.GetAttachmentNames(), attNames);
            NUnit.Framework.Assert.AreEqual("text/plain; charset=utf-8", attach.GetContentType
                ());
            NUnit.Framework.Assert.AreEqual(IOUtils.ToString(attach.GetContent(), "UTF-8"), content
                );
            NUnit.Framework.Assert.AreEqual(Sharpen.Runtime.GetBytesForString(content).Length
                , attach.GetLength());
            return doc;
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void StopReplication(Replication replication)
        {
            CountDownLatch replicationDoneSignal = new CountDownLatch(1);
            LiteTestCase.ReplicationStoppedObserver replicationStoppedObserver = new LiteTestCase.ReplicationStoppedObserver
                (replicationDoneSignal);
            replication.AddChangeListener(replicationStoppedObserver);
            replication.Stop();
            bool success = replicationDoneSignal.Await(30, TimeUnit.Seconds);
            NUnit.Framework.Assert.IsTrue(success);
            // give a little padding to give it a chance to save a checkpoint
            Sharpen.Thread.Sleep(2 * 1000);
        }

        public virtual void RunReplication(Replication replication)
        {
            CountDownLatch replicationDoneSignal = new CountDownLatch(1);
            LiteTestCase.ReplicationFinishedObserver replicationFinishedObserver = new LiteTestCase.ReplicationFinishedObserver
                (replicationDoneSignal);
            replication.AddChangeListener(replicationFinishedObserver);
            replication.Start();
            CountDownLatch replicationDoneSignalPolling = ReplicationWatcherThread(replication
                );
            Log.D(Tag, "Waiting for replicator to finish");
            try
            {
                bool success = replicationDoneSignal.Await(120, TimeUnit.Seconds);
                NUnit.Framework.Assert.IsTrue(success);
                success = replicationDoneSignalPolling.Await(120, TimeUnit.Seconds);
                NUnit.Framework.Assert.IsTrue(success);
                Log.D(Tag, "replicator finished");
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
            replication.RemoveChangeListener(replicationFinishedObserver);
        }

        public virtual CountDownLatch ReplicationWatcherThread(Replication replication)
        {
            CountDownLatch doneSignal = new CountDownLatch(1);
            new Sharpen.Thread(new _Runnable_435(replication, doneSignal)).Start();
            return doneSignal;
        }

        private sealed class _Runnable_435 : Runnable
        {
            public _Runnable_435(Replication replication, CountDownLatch doneSignal)
            {
                this.replication = replication;
                this.doneSignal = doneSignal;
            }

            public void Run()
            {
                bool started = false;
                bool done = false;
                while (!done)
                {
                    if (replication.IsRunning())
                    {
                        started = true;
                    }
                    bool statusIsDone = (replication.GetStatus() == Replication.ReplicationStatus.ReplicationStopped
                         || replication.GetStatus() == Replication.ReplicationStatus.ReplicationIdle);
                    if (started && statusIsDone)
                    {
                        done = true;
                    }
                    try
                    {
                        Sharpen.Thread.Sleep(500);
                    }
                    catch (Exception e)
                    {
                        Sharpen.Runtime.PrintStackTrace(e);
                    }
                }
                doneSignal.CountDown();
            }

            private readonly Replication replication;

            private readonly CountDownLatch doneSignal;
        }

        public class ReplicationFinishedObserver : Replication.ChangeListener
        {
            public bool replicationFinished = false;

            private CountDownLatch doneSignal;

            public ReplicationFinishedObserver(CountDownLatch doneSignal)
            {
                this.doneSignal = doneSignal;
            }

            public virtual void Changed(Replication.ChangeEvent @event)
            {
                Replication replicator = @event.GetSource();
                Log.D(Tag, replicator + " changed.  " + replicator.GetCompletedChangesCount() + " / "
                     + replicator.GetChangesCount());
                if (replicator.GetCompletedChangesCount() < 0)
                {
                    string msg = string.Format("%s: replicator.getCompletedChangesCount() < 0", replicator
                        );
                    Log.D(Tag, msg);
                    throw new RuntimeException(msg);
                }
                if (replicator.GetChangesCount() < 0)
                {
                    string msg = string.Format("%s: replicator.getChangesCount() < 0", replicator);
                    Log.D(Tag, msg);
                    throw new RuntimeException(msg);
                }
                // see https://github.com/couchbase/couchbase-lite-java-core/issues/100
                if (replicator.GetCompletedChangesCount() > replicator.GetChangesCount())
                {
                    string msg = string.Format("replicator.getCompletedChangesCount() - %d > replicator.getChangesCount() - %d"
                        , replicator.GetCompletedChangesCount(), replicator.GetChangesCount());
                    Log.D(Tag, msg);
                    throw new RuntimeException(msg);
                }
                if (!replicator.IsRunning())
                {
                    replicationFinished = true;
                    string msg = string.Format("ReplicationFinishedObserver.changed called, set replicationFinished to: %b"
                        , replicationFinished);
                    Log.D(Tag, msg);
                    doneSignal.CountDown();
                }
                else
                {
                    string msg = string.Format("ReplicationFinishedObserver.changed called, but replicator still running, so ignore it"
                        );
                    Log.D(Tag, msg);
                }
            }

            internal virtual bool IsReplicationFinished()
            {
                return replicationFinished;
            }
        }

        public class ReplicationRunningObserver : Replication.ChangeListener
        {
            private CountDownLatch doneSignal;

            public ReplicationRunningObserver(CountDownLatch doneSignal)
            {
                this.doneSignal = doneSignal;
            }

            public virtual void Changed(Replication.ChangeEvent @event)
            {
                Replication replicator = @event.GetSource();
                if (replicator.IsRunning())
                {
                    doneSignal.CountDown();
                }
            }
        }

        public class ReplicationIdleObserver : Replication.ChangeListener
        {
            private CountDownLatch doneSignal;

            public ReplicationIdleObserver(CountDownLatch doneSignal)
            {
                this.doneSignal = doneSignal;
            }

            public virtual void Changed(Replication.ChangeEvent @event)
            {
                Replication replicator = @event.GetSource();
                if (replicator.GetStatus() == Replication.ReplicationStatus.ReplicationIdle)
                {
                    doneSignal.CountDown();
                }
            }
        }

        public class ReplicationStoppedObserver : Replication.ChangeListener
        {
            private CountDownLatch doneSignal;

            public ReplicationStoppedObserver(CountDownLatch doneSignal)
            {
                this.doneSignal = doneSignal;
            }

            public virtual void Changed(Replication.ChangeEvent @event)
            {
                Replication replicator = @event.GetSource();
                if (replicator.GetStatus() == Replication.ReplicationStatus.ReplicationStopped)
                {
                    doneSignal.CountDown();
                }
            }
        }

        public class ReplicationErrorObserver : Replication.ChangeListener
        {
            private CountDownLatch doneSignal;

            public ReplicationErrorObserver(CountDownLatch doneSignal)
            {
                this.doneSignal = doneSignal;
            }

            public virtual void Changed(Replication.ChangeEvent @event)
            {
                Replication replicator = @event.GetSource();
                if (replicator.GetLastError() != null)
                {
                    doneSignal.CountDown();
                }
            }
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void DumpTableMaps()
        {
            Cursor cursor = database.GetDatabase().RawQuery("SELECT * FROM maps", null);
            while (cursor.MoveToNext())
            {
                int viewId = cursor.GetInt(0);
                int sequence = cursor.GetInt(1);
                byte[] key = cursor.GetBlob(2);
                string keyStr = null;
                if (key != null)
                {
                    keyStr = Sharpen.Runtime.GetStringForBytes(key);
                }
                byte[] value = cursor.GetBlob(3);
                string valueStr = null;
                if (value != null)
                {
                    valueStr = Sharpen.Runtime.GetStringForBytes(value);
                }
                Log.D(Tag, string.Format("Maps row viewId: %s seq: %s, key: %s, val: %s", viewId, 
                    sequence, keyStr, valueStr));
            }
        }

        /// <exception cref="System.Exception"></exception>
        public virtual void DumpTableRevs()
        {
            Cursor cursor = database.GetDatabase().RawQuery("SELECT * FROM revs", null);
            while (cursor.MoveToNext())
            {
                int sequence = cursor.GetInt(0);
                int doc_id = cursor.GetInt(1);
                byte[] revid = cursor.GetBlob(2);
                string revIdStr = null;
                if (revid != null)
                {
                    revIdStr = Sharpen.Runtime.GetStringForBytes(revid);
                }
                int parent = cursor.GetInt(3);
                int current = cursor.GetInt(4);
                int deleted = cursor.GetInt(5);
                Log.D(Tag, string.Format("Revs row seq: %s doc_id: %s, revIdStr: %s, parent: %s, current: %s, deleted: %s"
                    , sequence, doc_id, revIdStr, parent, current, deleted));
            }
        }

        /// <exception cref="System.Exception"></exception>
        public static SavedRevision CreateRevisionWithRandomProps(SavedRevision createRevFrom
            , bool allowConflict)
        {
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Put(UUID.RandomUUID().ToString(), "val");
            UnsavedRevision unsavedRevision = createRevFrom.CreateRevision();
            unsavedRevision.SetUserProperties(properties);
            return unsavedRevision.Save(allowConflict);
        }
    }
}

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
using System.IO;
using Apache.Http;
using Apache.Http.Client;
using Apache.Http.Client.Methods;
using Apache.Http.Entity;
using Apache.Http.Impl.Client;
using Apache.Http.Message;
using Couchbase.Lite;
using Couchbase.Lite.Performance;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Threading;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Org.Apache.Commons.IO;
using Org.Apache.Commons.IO.Output;
using Sharpen;

namespace Couchbase.Lite.Performance
{
    public class Test7_PullReplication : LiteTestCase
    {
        public const string Tag = "PullReplicationPerformance";

        private const string _propertyValue = "1234567";

        /// <exception cref="System.Exception"></exception>
        protected override void SetUp()
        {
            Log.V(Tag, "DeleteDBPerformance setUp");
            base.SetUp();
            string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
            for (int i = 0; i < GetNumberOfDocuments(); i++)
            {
                string docId = string.Format("doc%d-%s", i, docIdTimestamp);
                Log.D(Tag, "Adding " + docId + " directly to sync gateway");
                try
                {
                    AddDocWithId(docId, "attachment.png", false);
                }
                catch (IOException ioex)
                {
                    Log.E(Tag, "Add document directly to sync gateway failed", ioex);
                    Fail();
                }
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestPullReplicationPerformance()
        {
            long startMillis = Runtime.CurrentTimeMillis();
            Log.D(Tag, "testPullReplicationPerformance() started");
            DoPullReplication();
            NUnit.Framework.Assert.IsNotNull(database);
            Log.D(Tag, "testPullReplicationPerformance() finished");
            Log.V("PerformanceStats", Tag + "," + Sharpen.Extensions.ValueOf(Runtime.CurrentTimeMillis
                () - startMillis).ToString() + "," + GetNumberOfDocuments());
        }

        private void DoPullReplication()
        {
            Uri remote = GetReplicationURL();
            Replication repl = (Replication)database.CreatePullReplication(remote);
            repl.SetContinuous(false);
            Log.D(Tag, "Doing pull replication with: " + repl);
            RunReplication(repl);
            Log.D(Tag, "Finished pull replication with: " + repl);
        }

        /// <exception cref="System.IO.IOException"></exception>
        private void AddDocWithId(string docId, string attachmentName, bool gzipped)
        {
            string docJson;
            if (attachmentName != null)
            {
                // add attachment to document
                InputStream attachmentStream = GetAsset(attachmentName);
                ByteArrayOutputStream baos = new ByteArrayOutputStream();
                IOUtils.Copy(attachmentStream, baos);
                if (gzipped == false)
                {
                    string attachmentBase64 = Base64.EncodeBytes(baos.ToByteArray());
                    docJson = string.Format("{\"foo\":1,\"bar\":false, \"_attachments\": { \"%s\": { \"content_type\": \"image/png\", \"data\": \"%s\" } } }"
                        , attachmentName, attachmentBase64);
                }
                else
                {
                    byte[] bytes = baos.ToByteArray();
                    string attachmentBase64 = Base64.EncodeBytes(bytes, Base64.Gzip);
                    docJson = string.Format("{\"foo\":1,\"bar\":false, \"_attachments\": { \"%s\": { \"content_type\": \"image/png\", \"data\": \"%s\", \"encoding\": \"gzip\", \"length\":%d } } }"
                        , attachmentName, attachmentBase64, bytes.Length);
                }
            }
            else
            {
                docJson = "{\"foo\":1,\"bar\":false}";
            }
            PushDocumentToSyncGateway(docId, docJson);
            WorkaroundSyncGatewayRaceCondition();
        }

        /// <exception cref="System.UriFormatException"></exception>
        private void PushDocumentToSyncGateway(string docId, string docJson)
        {
            // push a document to server
            Uri replicationUrlTrailingDoc1 = new Uri(string.Format("%s/%s", GetReplicationURL
                ().ToExternalForm(), docId));
            Uri pathToDoc1 = new Uri(replicationUrlTrailingDoc1, docId);
            Log.D(Tag, "Send http request to " + pathToDoc1);
            CountDownLatch httpRequestDoneSignal = new CountDownLatch(1);
            BackgroundTask getDocTask = new _BackgroundTask_139(pathToDoc1, docJson, httpRequestDoneSignal
                );
            getDocTask.Execute();
            Log.D(Tag, "Waiting for http request to finish");
            try
            {
                httpRequestDoneSignal.Await(300, TimeUnit.Seconds);
                Log.D(Tag, "http request finished");
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
        }

        private sealed class _BackgroundTask_139 : BackgroundTask
        {
            public _BackgroundTask_139(Uri pathToDoc1, string docJson, CountDownLatch httpRequestDoneSignal
                )
            {
                this.pathToDoc1 = pathToDoc1;
                this.docJson = docJson;
                this.httpRequestDoneSignal = httpRequestDoneSignal;
            }

            public override void Run()
            {
                HttpClient httpclient = new DefaultHttpClient();
                HttpResponse response;
                string responseString = null;
                try
                {
                    HttpPut post = new HttpPut(pathToDoc1.ToExternalForm());
                    StringEntity se = new StringEntity(docJson.ToString());
                    se.SetContentType(new BasicHeader("content_type", "application/json"));
                    post.SetEntity(se);
                    response = httpclient.Execute(post);
                    StatusLine statusLine = response.GetStatusLine();
                    Log.D(Test7_PullReplication.Tag, "Got response: " + statusLine);
                    NUnit.Framework.Assert.IsTrue(statusLine.GetStatusCode() == HttpStatus.ScCreated);
                }
                catch (ClientProtocolException e)
                {
                    NUnit.Framework.Assert.IsNull("Got ClientProtocolException: " + e.GetLocalizedMessage
                        (), e);
                }
                catch (IOException e)
                {
                    NUnit.Framework.Assert.IsNull("Got IOException: " + e.GetLocalizedMessage(), e);
                }
                httpRequestDoneSignal.CountDown();
            }

            private readonly Uri pathToDoc1;

            private readonly string docJson;

            private readonly CountDownLatch httpRequestDoneSignal;
        }

        /// <summary>
        /// Whenever posting information directly to sync gateway via HTTP, the client
        /// must pause briefly to give it a chance to achieve internal consistency.
        /// </summary>
        /// <remarks>
        /// Whenever posting information directly to sync gateway via HTTP, the client
        /// must pause briefly to give it a chance to achieve internal consistency.
        /// <p/>
        /// This is documented in https://github.com/couchbase/sync_gateway/issues/228
        /// </remarks>
        private void WorkaroundSyncGatewayRaceCondition()
        {
            try
            {
                Sharpen.Thread.Sleep(5 * 1000);
            }
            catch (Exception e)
            {
                Sharpen.Runtime.PrintStackTrace(e);
            }
        }

        private int GetNumberOfDocuments()
        {
            return System.Convert.ToInt32(Runtime.GetProperty("Test7_numberOfDocuments"));
        }
    }
}

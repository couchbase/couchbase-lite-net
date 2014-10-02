//
// PerformanceTestCase.cs
//
// Author:
//     Pasin Suriyentrakorn  <pasin@couchbase.com>
//
// Copyright (c) 2014 Couchbase Inc
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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Sharpen;

namespace Couchbase.Lite
{
    public abstract class PerformanceTestCase : LiteTestCase
    {
        protected const string NUMDOCS_KEY = "num_docs";
        protected const string DOCSIZE_KEY = "doc_size";

        public delegate long Test(IDictionary<string, object> parameters);

        private JObject _config;

        public PerformanceTestCase()
        {
        }

        [SetUp]
        protected void InitConfig()
        {
            // Workaround for filtering only performance log
            Log.SetLogger(null);

            var stream = GetAsset("performance-test.json");
            var reader = new StreamReader(stream);
            _config = JObject.Parse(reader.ReadToEnd());
        }

        [TearDown]
        protected void Cleanup()
        {
            Log.SetLogger(LoggerFactory.CreateLogger());
        }

        private void PrintMessage(string tag, string message) {
            Console.WriteLine(String.Format("[{0}] {1}", tag, message));
        }

        protected IDictionary<string, object> CreateTestProperties(Int32 size)
        {
            var bigObj = new string[size];
            for (var i = 0; i < bigObj.Length; i++)
            {
                bigObj[i] = "1";
            }

            var properties = new Dictionary<string, object>() {
                {"bigArray", bigObj}
            };

            return properties;
        }

        protected void AddDoc(string docId, IDictionary<string, object> properties, 
            string attachmentName, string attachmentContentType)
        {
            if (!String.IsNullOrWhiteSpace(attachmentName))
            {
                var attachmentStream = (InputStream)GetAsset(attachmentName);
                var memStream = new MemoryStream();
                attachmentStream.Wrapped.CopyTo(memStream);
                var attachmentBase64 = Convert.ToBase64String(memStream.ToArray());

                var attachment = new Dictionary<string, object>();
                attachment["content_type"] = attachmentContentType;
                attachment["data"] = attachmentBase64;

                var attachments = new Dictionary<string, object>();
                attachments[attachmentName] = attachment;

                properties["_attachments"] = attachments;
            }

            var doc = database.GetDocument(docId);
            doc.PutProperties(properties);
        }

        private void PushDocumentToSyncGateway(string docId, string docJson)
        {
            var url = new Uri(string.Format("{0}/{1}", GetReplicationURL(), docId));
            var doneSignal = new CountDownLatch(1);
            Task.Factory.StartNew(() =>
            {
                HttpClient httpclient = null;
                try
                {
                    httpclient = new HttpClient();
                    var request = new HttpRequestMessage();
                    request.Headers.Add("Accept", "*/*");
                    var postTask = httpclient.PutAsync(url.AbsoluteUri, 
                        new StringContent(docJson, Encoding.UTF8, "application/json"));
                    var response = postTask.Result;
                    Assert.IsTrue(response.StatusCode == HttpStatusCode.Created);
                }
                catch (Exception e)
                {
                    Assert.IsNull(e, "Got IOException: " + e.Message);
                }
                finally
                {
                    httpclient.Dispose();
                }
                doneSignal.CountDown();
            });

            var success = doneSignal.Await(TimeSpan.FromSeconds(10));
            Assert.IsTrue(success);
        }

        protected void AddDocToSyncGateway(string docId, IDictionary<string, object> properties, 
            string attachmentName, string attachmentContentType)
        {
            if (!String.IsNullOrWhiteSpace(attachmentName))
            {
                var attachmentStream = (InputStream)GetAsset(attachmentName);
                var memStream = new MemoryStream();
                attachmentStream.Wrapped.CopyTo(memStream);
                var attachmentBase64 = Convert.ToBase64String(memStream.ToArray());

                var attachment = new Dictionary<string, object>();
                attachment["content_type"] = attachmentContentType;
                attachment["data"] = attachmentBase64;

                var attachments = new Dictionary<string, object>();
                attachments[attachmentName] = attachment;

                properties["_attachments"] = attachments;
            }

            var docJson = Manager.GetObjectMapper().WriteValueAsString(properties);
            PushDocumentToSyncGateway(docId, docJson);
        }

        private static CountdownEvent ReplicationWatcherThread(Replication replication)
        {
            var started = replication.IsRunning;
            var doneSignal = new CountdownEvent(1);

            Task.Factory.StartNew(()=>
            {
                var done = false;
                while (!done)
                {
                    started |= replication.IsRunning;

                    var statusIsDone = (
                        replication.Status == ReplicationStatus.Stopped 
                        || replication.Status == ReplicationStatus.Idle
                    );

                    if (started && statusIsDone)
                    {
                        done = true;
                    }

                    Thread.Sleep(100);
                }
                doneSignal.Signal();
            });

            return doneSignal;
        }

        protected void RunReplication(Replication replication)
        {
            var replicationDoneSignal = new CountDownLatch(1);
            var observer = new ReplicationObserver(replicationDoneSignal);
            replication.Changed += observer.Changed;
            replication.Start();

            var replicationDoneSignalPolling = ReplicationWatcherThread(replication);

            var success = replicationDoneSignal.Await(TimeSpan.FromSeconds(15));
            Assert.IsTrue(success);
            success = replicationDoneSignalPolling.Wait(TimeSpan.FromSeconds(15));
            Assert.IsTrue(success);

            replication.Changed -= observer.Changed;
        }

        protected void RunTest(string name, Test test)
        {
            var enabled = (Boolean)_config["enabled"];
            if (!enabled)
            {
                PrintMessage(name, "Performance tests are not enabled. " +
                    "Enabled performance tests on performance-test.json.");
                return;
            }

            JObject config = (JObject)_config[name];
            if (config == null)
            {
                PrintMessage(name, "No configuration of the test found.");
                return;
            }

            enabled = (Boolean)config["enabled"];
            if (!enabled)
            {
                PrintMessage(name, "Test is not enabled. " +
                    "Enabled the test on performance-test.json.");
                return;
            }

            var numDocs = (JArray)config["numbers_of_documents"];
            var docSizes = (JArray)config["sizes_of_document"];
            var kpis = (JArray)config["kpi"];
            var baselines = (JArray)config["baseline"];
            var needsPerDocResult = config["kpi_is_total"] != null && !(Boolean)config["kpi_is_total"];
            var repeatCount = (Int32)config["repeat_count"];

            double[,] finalResults = new double[numDocs.Count, docSizes.Count];
            double[,] baselineDelta = new double[numDocs.Count, docSizes.Count];

            var testCount = 0;
            var failCount = 0;
            for (var i = 0; i < numDocs.Count; i++)
            {
                for (var j = 0; j < docSizes.Count; j++)
                {
                    testCount++;
                    var numDoc = (Int32)numDocs[i];
                    var docSize = (Int32)docSizes[j];
                    var kpi = (double)((JArray)kpis[i])[j];
                    if (kpi < 0 || numDoc < 0 || docSize < 0)
                    {
                        finalResults[i, j] = kpi;
                        baselineDelta[i, j] = -1.0;
                        PrintMessage(name, String.Format(
                            "skip: #test {0}, #docs {1}, size {2}B", 
                            testCount, numDoc, docSize));
                        continue;
                    }

                    double[] results = new double[repeatCount];
                    for (var k = 0; k < repeatCount; k++)
                    {
                        base.TearDown();
                        Thread.Sleep(2000);
                        base.SetUp();
                        Thread.Sleep(2000);

                        // Execute test
                        var parameters = new Dictionary<string, object>() 
                        {
                            {NUMDOCS_KEY, numDoc},
                            {DOCSIZE_KEY, docSize}
                        };
                        var time = test(parameters);
                        results[k] = time;
                    }
                        
                    var min = results.Min<double>();
                    var result = needsPerDocResult ? (min / numDoc) : min;
                    finalResults[i, j] = result;

                    var baseline = (double)((JArray)baselines[i])[j];
                    var deltaFromBaseline = (result - baseline) / baseline * 100;
                    baselineDelta[i, j] = deltaFromBaseline;

                    Boolean pass = result <= kpi && deltaFromBaseline <= 20;
                    if (!pass)
                    {
                        failCount++;
                    }

                    var message = String.Format(
                        "stats: #test {0}, pass {1}, #docs {2}, size {3}B, " +
                        "avg {4:F2}, max {5:F2}, min {6:F2}, " +
                        "result {7:F2}, kpi {8:F2}, " +
                        "baseline {9:F2}, diffBaseLine {10:F2}%, " +
                        "allResult {11}", 
                        testCount, pass, numDoc, docSize, 
                        results.Average(), results.Max(), results.Min(), 
                        result, kpi, baseline, deltaFromBaseline, 
                        Manager.GetObjectMapper().WriteValueAsString(results));
                    PrintMessage(name, message);
                }
            }

            // Print Summary
            PrintMessage(name, "Result:");
            PrintMessage(name, String.Format(
                "summary: pass {0}, #test {1}, #fail {2}", 
                (failCount == 0), testCount, failCount));

            // Print Result Table
            var header = new StringBuilder("#docs,");
            for (var i = 0; i < docSizes.Count; i++)
            {
                if (i > 0) header.Append(",");
                header.Append(docSizes[i]);
            }
            PrintMessage(Tag, header.ToString());
            for (var i = 0; i < numDocs.Count; i++)
            {
                var sb = new StringBuilder(String.Format("{0}", numDocs[i]));
                for (var j = 0; j < finalResults.GetLength(1); j++)
                {
                    sb.Append(",");
                    sb.Append(String.Format("{0:F2}", finalResults[i, j]));
                }
                PrintMessage(Tag, sb.ToString());
            }

            // Print delta from baselines
            PrintMessage(name, "Percentage of Deviation from baselines:");
            PrintMessage(Tag, header.ToString());
            for (var i = 0; i < numDocs.Count; i++)
            {
                var sb = new StringBuilder(String.Format("{0}", numDocs[i]));
                for (var j = 0; j < baselineDelta.GetLength(1); j++)
                {
                    sb.Append(",");
                    sb.Append(String.Format("{0:F2}", baselineDelta[i, j]));
                }
                PrintMessage(Tag, sb.ToString());
            }

            Assert.IsTrue(failCount == 0);
        }
    }
}

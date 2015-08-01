//
//  SyncGateway.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
using System;
using System.Net;
using NUnit.Framework;
using System.Text;
using Sharpen;
using System.IO;
using Couchbase.Lite.Util;
using System.Net.Http;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Couchbase.Lite.Tests
{
    public sealed class RemoteDatabase : IDisposable
    {
        private const string Tag = "RemoteDatabase";
        private readonly IRemoteEndpoint _parent;
        private readonly Uri _adminRemoteUri;
        private readonly Uri _remoteUri;
        private readonly HttpClient _httpClient;
        public readonly string Name;

        public Uri RemoteUri
        {
            get { return _remoteUri; }
        }

        internal RemoteDatabase(IRemoteEndpoint parent, string name)
        {
            _parent = parent;
            _adminRemoteUri = parent.AdminUri.AppendPath(name);
            _remoteUri = parent.RegularUri.AppendPath(name);
            var handler = new HttpClientHandler { Credentials = new NetworkCredential("jim", "borden") };
            handler.PreAuthenticate = true;
            _httpClient = new HttpClient(handler, true);
            Name = name;
        }

        public void VerifyDocumentExists(string docId) 
        {
            var pathToDoc = _remoteUri.AppendPath(docId);
            Log.D(Tag, "Send http request to " + pathToDoc);

            var httpRequestDoneSignal = new CountdownEvent(1);
            Task.Factory.StartNew(() =>
            {
                HttpResponseMessage response;
                string responseString = null;
                try
                {
                    var responseTask = _httpClient.GetAsync(pathToDoc.ToString());
                    response = responseTask.Result;
                    var statusLine = response.StatusCode;
                    Assert.IsTrue(statusLine == HttpStatusCode.OK);
                    if (statusLine == HttpStatusCode.OK)
                    {
                        var responseStringTask = response.Content.ReadAsStringAsync();
                        responseStringTask.Wait(TimeSpan.FromSeconds(10));
                        responseString = responseStringTask.Result;
                        Assert.IsTrue(responseString.Contains(docId));
                        Log.D(Tag, "result: " + responseString);
                    }
                    else
                    {
                        var statusReason = response.ReasonPhrase;
                        response.Dispose();
                        throw new IOException(statusReason);
                    }
                }
                catch (ProtocolViolationException e)
                {
                    Assert.IsNull(e, "Got ClientProtocolException: " + e.Message);
                }
                catch (IOException e)
                {
                    Assert.IsNull(e, "Got IOException: " + e.Message);
                }

                httpRequestDoneSignal.Signal();
            });

            var result = httpRequestDoneSignal.Wait(TimeSpan.FromSeconds(30));
            Assert.IsTrue(result, "Could not retrieve the new doc from the sync gateway.");
        }

        public void DisableGuestAccess()
        {
            var url = _adminRemoteUri.AppendPath("_user/GUEST");
            var request = WebRequest.CreateHttp(url);
            request.Method = "PUT";
            var data = Manager.GetObjectMapper().WriteValueAsBytes(new Dictionary<string, object> { { "disabled", true } }).ToArray();
            request.GetRequestStream().Write(data, 0, data.Length);
            request.GetResponse();
        }

        public void RestoreGuestAccess()
        {
            var url = _adminRemoteUri.AppendPath("_user/GUEST");
            var request = WebRequest.CreateHttp(url);
            request.Method = "PUT";
            var data = Manager.GetObjectMapper().WriteValueAsBytes(new Dictionary<string, object> { 
                { "disabled", false },
                { "admin_channels", new List<object> { "*" }}
            }).ToArray();
            request.GetRequestStream().Write(data, 0, data.Length);
            request.GetResponse();
        }

        public HashSet<string> AddDocuments(int count, bool withAttachment)
        {
            var docList = new HashSet<string>();
            var json = CreateDocumentJson(withAttachment ? "attachment.png" : null).Substring(1);
            var beginning = Encoding.UTF8.GetBytes(@"{""docs"":[");

            var request = HttpWebRequest.CreateHttp(_remoteUri.AppendPath("_bulk_docs"));
            request.Method = "POST";
            var requestStream = request.GetRequestStream();
            requestStream.Write(beginning, 0, beginning.Length);

            for (int i = 0; i < count; i++) {
                var docIdTimestamp = Convert.ToString(Runtime.CurrentTimeMillis());
                var docId = string.Format("doc{0}-{1}", i, docIdTimestamp);         

                docList.Add(docId);
                string nextJson = null;
                if(i != count-1) {
                    nextJson = String.Format(@"{{""_id"":""{0}"",{1},", docId, json);
                } else {
                    nextJson = String.Format(@"{{""_id"":""{0}"",{1}]}}", docId, json);
                }

                var jsonBytes = Encoding.UTF8.GetBytes(nextJson);
                requestStream.Write(jsonBytes, 0, jsonBytes.Length);
            }
                
            var response = request.GetResponse();
            Assert.AreEqual(HttpStatusCode.Created, ((HttpWebResponse)response).StatusCode);

            return docList;
        }

        public void AddDocument(string docId, string attachmentName)
        {
            string docJson = CreateDocumentJson(attachmentName);

            // push a document to server
            var replicationUrlTrailingDoc1 = new Uri(string.Format("{0}/{1}", _remoteUri, docId));
            var pathToDoc1 = new Uri(replicationUrlTrailingDoc1, docId);
            Log.D(Tag, "Send http request to " + pathToDoc1);
            try
            {
                HttpResponseMessage response;
                var request = new HttpRequestMessage();
                request.Headers.Add("Accept", "*/*");

                var postTask = _httpClient.PutAsync(pathToDoc1.AbsoluteUri, new StringContent(docJson, Encoding.UTF8, "application/json"));
                response = postTask.Result;
                var statusLine = response.StatusCode;
                Log.D(Tag, "Got response: " + statusLine);
                Assert.IsTrue(statusLine == HttpStatusCode.Created);
            }
            catch (ProtocolViolationException e)
            {
                Assert.IsNull(e, "Got ClientProtocolException: " + e.Message);
            }
            catch (IOException e)
            {
                Assert.IsNull(e, "Got IOException: " + e.Message);
            }

            WorkaroundSyncGatewayRaceCondition();
        }

        private string CreateDocumentJson(string attachmentName)
        {
            if (attachmentName != null)
            {
                // add attachment to document
                var attachmentStream = (InputStream)GetAsset(attachmentName);
                var baos = new MemoryStream();
                attachmentStream.Wrapped.CopyTo(baos);
                attachmentStream.Dispose();
                var attachmentBase64 = Convert.ToBase64String(baos.ToArray());
                baos.Dispose();
                return String.Format("{{\"foo\":1,\"bar\":false, \"_attachments\": {{ \"i_use_couchdb.png\": {{ \"content_type\": \"image/png\", \"data\": \"{0}\" }} }} }}", attachmentBase64);
            }
            else
            {
                return @"{""foo"":1,""bar"":false}";
            }
        }

        private Stream GetAsset(string name)
        {
            var assetPath = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".Assets." + name;
            Log.D(Tag, "Fetching assembly resource: " + assetPath);
            var stream = GetType().GetResourceAsStream(assetPath);
            return stream;
        }

        private void WorkaroundSyncGatewayRaceCondition() 
        {
            Thread.Sleep(2 * 100);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _parent.DeleteDatabase(this);
        }
    }

    public sealed class SyncGateway : IRemoteEndpoint
    {
        private readonly Uri _regularUri;
        private readonly Uri _adminUri;

        public Uri RegularUri
        {
            get { return _regularUri; }
        }

        public Uri AdminUri
        {
            get { return _adminUri; }
        }

        public SyncGateway(string protocol, string server)
        {
            var builder = new UriBuilder();
            builder.Scheme = protocol;
            builder.Host = server;
            builder.Port = 4984;
            _regularUri = builder.Uri;

            builder.Port = 4985;
            _adminUri = builder.Uri;
        }

        public RemoteDatabase CreateDatabase(string name)
        {
            var server = _adminUri.AppendPath(name);
            var putRequest = HttpWebRequest.Create(server);
            putRequest.Method = "PUT";
            putRequest.ContentType = "application/json";
            var body = Encoding.UTF8.GetBytes(@"{""server"":""walrus:"",
                 ""users"": {
                    ""GUEST"": {""disabled"": false, ""admin_channels"": [""*""]},
                    ""jim"" : { ""password"": ""borden"", ""admin_channels"": [""*""]}
                  },
                 ""bucket"":""" + name + @""",
                 ""sync"":""function(doc) {channel(doc.channels);}""}");
            putRequest.GetRequestStream().Write(body, 0, body.Length);

            try {
                var putResponse = (HttpWebResponse)putRequest.GetResponse();
                Assert.AreEqual(HttpStatusCode.Created, putResponse.StatusCode);
            } catch(WebException ex) {
                if (ex.Status == WebExceptionStatus.ProtocolError) {
                    var response = ex.Response as HttpWebResponse;
                    if (response != null) {
                        Assert.AreEqual(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    } else {
                        Assert.Fail("Error from Sync Gateway: {0}", response.StatusCode);
                    }
                } else {
                    Assert.Fail("Error from Sync Gateway: {0}", ex);
                }
            }

            return new RemoteDatabase(this, name);
        }

        public void DeleteDatabase(RemoteDatabase db)
        {
            Task.Delay(1000).ContinueWith(t =>
            {
                var server = _adminUri.AppendPath(db.Name);
                var deleteRequest = HttpWebRequest.Create(server);
                deleteRequest.Method = "DELETE";
                try {
                    var response = (HttpWebResponse)deleteRequest.GetResponse();
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                } catch (WebException ex) {
                    if (ex.Status == WebExceptionStatus.ProtocolError) {
                        var response = ex.Response as HttpWebResponse;
                        if (response != null) {
                            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
                        } else {
                            Assert.Fail("Error from Sync Gateway: {0}", response.StatusCode);
                        }
                    } else {
                        Assert.Fail("Error from Sync Gateway: {0}", ex);
                    }
                }
            });
        }
    }
}


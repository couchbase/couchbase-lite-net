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

namespace Couchbase.Lite.Tests
{
    public sealed class RemoteDatabase : IDisposable
    {
        private const string Tag = "RemoteDatabase";
        private readonly SyncGateway _parent;
        public readonly Uri RemoteUri;
        public readonly string Name;

        internal RemoteDatabase(SyncGateway parent, string name)
        {
            _parent = parent;
            RemoteUri = parent.RegularUri.AppendPath(name);
            Name = name;
        }

        public void AddDocument(string docId, string attachmentName)
        {
            string docJson;
            if (attachmentName != null)
            {
                // add attachment to document
                var attachmentStream = (InputStream)GetAsset(attachmentName);
                var baos = new MemoryStream();
                attachmentStream.Wrapped.CopyTo(baos);
                attachmentStream.Dispose();
                var attachmentBase64 = Convert.ToBase64String(baos.ToArray());
                baos.Dispose();
                docJson = String.Format("{{\"foo\":1,\"bar\":false, \"_attachments\": {{ \"i_use_couchdb.png\": {{ \"content_type\": \"image/png\", \"data\": \"{0}\" }} }} }}", attachmentBase64);
            }
            else
            {
                docJson = @"{""foo"":1,""bar"":false}";
            }

            // push a document to server
            var replicationUrlTrailingDoc1 = new Uri(string.Format("{0}/{1}", RemoteUri, docId));
            var pathToDoc1 = new Uri(replicationUrlTrailingDoc1, docId);
            Log.D(Tag, "Send http request to " + pathToDoc1);
            HttpClient httpclient = null;
            try
            {
                var handler = new HttpClientHandler { Credentials = new NetworkCredential("jim", "borden") };
                handler.PreAuthenticate = true;
                httpclient = new HttpClient(handler, true);

                HttpResponseMessage response;
                var request = new HttpRequestMessage();
                request.Headers.Add("Accept", "*/*");

                var postTask = httpclient.PutAsync(pathToDoc1.AbsoluteUri, new StringContent(docJson, Encoding.UTF8, "application/json"));
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
            finally
            {
                httpclient.Dispose();
            }

            WorkaroundSyncGatewayRaceCondition();
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
            _parent.DeleteDatabase(this);
        }
    }

    public sealed class SyncGateway
    {
        public readonly Uri RegularUri;
        private readonly Uri _adminUri;

        public SyncGateway(string protocol, string server)
        {
            var builder = new UriBuilder();
            builder.Scheme = protocol;
            builder.Host = server;
            builder.Port = 4984;
            RegularUri = builder.Uri;

            builder.Port = 4985;
            _adminUri = builder.Uri;
        }

        public void DisableGuestAccess()
        {
            var url = _adminUri.AppendPath("_user/GUEST");
            var request = WebRequest.CreateHttp(url);
            request.Method = "PUT";
            var data = Manager.GetObjectMapper().WriteValueAsBytes(new Dictionary<string, object> { { "disabled", true } }).ToArray();
            request.GetRequestStream().Write(data, 0, data.Length);
            request.GetResponse();
        }

        public void RestoreGuestAccess()
        {
            var url = _adminUri.AppendPath("_user/GUEST");
            var request = WebRequest.CreateHttp(url);
            request.Method = "PUT";
            var data = Manager.GetObjectMapper().WriteValueAsBytes(new Dictionary<string, object> { 
                { "disabled", false },
                { "admin_channels", new List<object> { "*" }}
            }).ToArray();
            request.GetRequestStream().Write(data, 0, data.Length);
            request.GetResponse();
        }

        public RemoteDatabase CreateDatabase(string name)
        {
            var server = new Uri(_adminUri.AbsoluteUri + name);
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

            var putResponse = (HttpWebResponse)putRequest.GetResponse();
            Assert.AreEqual(HttpStatusCode.Created, putResponse.StatusCode);
            return new RemoteDatabase(this, name);
        }

        public void DeleteDatabase(RemoteDatabase db)
        {
            var server = new Uri(_adminUri.AbsoluteUri + db.Name);
            var deleteRequest = HttpWebRequest.Create(server);
            deleteRequest.Method = "DELETE";
            try {
                var response = (HttpWebResponse)deleteRequest.GetResponse();
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            } catch(WebException ex) {
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
        }
    }
}


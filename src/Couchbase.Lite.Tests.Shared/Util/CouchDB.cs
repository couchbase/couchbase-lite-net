//
//  CouchDB.cs
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
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Tests
{
    public sealed class CouchDB : IRemoteEndpoint
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

        public CouchDB(string protocol, string server)
        {
            var builder = new UriBuilder();
            builder.Scheme = protocol;
            builder.Host = server;
            builder.Port = 5984;
            _regularUri = builder.Uri;

            builder.Port = 5985;
            _adminUri = builder.Uri;
        }

        public RemoteDatabase CreateDatabase(string name)
        {
            var dbUri = _regularUri.AppendPath(name);

            try {
                HttpWebRequest.Create(_regularUri).GetResponse();
            } catch(Exception) {
                Assert.Inconclusive("Apache CouchDB not running");
            }

            var putRequest = HttpWebRequest.Create(dbUri);
            putRequest.Method = "PUT";
            var putResponse = (HttpWebResponse)putRequest.GetResponse();
            Assert.AreEqual(HttpStatusCode.Created, putResponse.StatusCode);
            return new RemoteDatabase(this, name);
        }

        public void DeleteDatabase(RemoteDatabase db)
        {
            var server = _regularUri.AppendPath(db.Name);
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
                        Assert.Fail("Error from CouchDB: {0}", response.StatusCode);
                    }
                } else {
                    Assert.Fail("Error from CouchDB: {0}", ex);
                }
            }
        }
    }
}


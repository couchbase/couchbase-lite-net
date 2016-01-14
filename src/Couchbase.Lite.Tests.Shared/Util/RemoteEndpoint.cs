//
//  RemoteEndpoint.cs
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
using System.Net.Http;
using System.Net;
using NUnit.Framework;
using System.Collections.Generic;

#if NET_3_5
using WebException = System.Net.Couchbase.WebException;
#endif

namespace Couchbase.Lite.Tests
{
    public abstract class RemoteEndpoint
    {
        private readonly Uri _regularUri;
        private readonly Uri _adminUri;

        protected abstract string Type { get; }

        public Uri RegularUri
        {
            get { return _regularUri; }
        }

        public Uri AdminUri
        {
            get { return _adminUri; }
        }

        protected RemoteEndpoint(string protocol, string server, int regularPort, int adminPort)
        {
            var builder = new UriBuilder();
            builder.Scheme = protocol;
            builder.Host = server;
            builder.Port = regularPort;
            _regularUri = builder.Uri;

            builder.Port = adminPort;
            _adminUri = builder.Uri;

            using (var httpClient = new HttpClient()) {
                try {
                    var request = new HttpRequestMessage(HttpMethod.Get, _adminUri + "/");
                    var response = httpClient.SendAsync(request).Result;
                    request = new HttpRequestMessage(HttpMethod.Get, _regularUri + "/");
                    response = httpClient.SendAsync(request).Result;
                } catch(AggregateException e) {
                    var ex = e.InnerException as WebException;
                    if (ex != null && ex.Status == WebExceptionStatus.ConnectFailure) {
                        Assert.Inconclusive("Failed to get connection to {0}", Type);
                    }
                }
            }
        }

        public virtual RemoteDatabase CreateDatabase(string name, string user = "jim", string password = "borden")
        {
            var retVal = new RemoteDatabase(this, name, user, password);
            retVal.Create();
            return retVal;
        }
    }
}


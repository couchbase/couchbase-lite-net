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

namespace Couchbase.Lite.Tests
{
    public abstract class RemoteEndpoint
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

        protected RemoteEndpoint(string protocol, string server, int regularPort, int adminPort)
        {
            var builder = new UriBuilder();
            builder.Scheme = protocol;
            builder.Host = server;
            builder.Port = regularPort;
            _regularUri = builder.Uri;

            builder.Port = adminPort;
            _adminUri = builder.Uri;
        }

        public virtual RemoteDatabase CreateDatabase(string name)
        {
            var retVal = new RemoteDatabase(this, name, "jim", "borden");
            retVal.Create();
            return retVal;
        }
    }
}


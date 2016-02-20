//
//  DigestAuthHeaderValue.cs
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
using System.Collections.Generic;
using System.Net;

using Couchbase.Lite.Auth;

namespace Couchbase.Lite.Listener.Tcp
{
    [Obsolete("This class is no longer needed, and will be removed")]
    public sealed class DigestAuthHeaderValue
    {
        private readonly IDictionary<string, string> _components = new Dictionary<string, string>();
        private string _calculatedResponse = String.Empty;

        internal string Nonce {
            get {
                return _components.Get("nonce");
            }
        }

        internal int Nc {
            get {
                return ExtensionMethods.CastOrDefault<int>(_components.Get("nc"));
            }
        }

        public DigestAuthHeaderValue(HttpListenerContext context)
        {
            var headerValue = context.Request.Headers["Authorization"];
            _components = DigestCalculator.ParseIntoComponents(headerValue);
            _components["method"] = context.Request.HttpMethod;
        }

        internal bool ValidateAgainst(CouchbaseLiteServiceListener listener)
        {
            CalculateResponse(listener);
            return _calculatedResponse == _components.Get("response");
        }

        private void CalculateResponse(CouchbaseLiteServiceListener listener)
        {
            if (!String.IsNullOrEmpty(_calculatedResponse)) {
                return;
            }

            _calculatedResponse = DigestCalculator.Calculate(_components, listener.HashPasswordToDigest);
        }
    }
}


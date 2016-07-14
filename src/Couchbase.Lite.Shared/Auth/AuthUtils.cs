//
//  AuthUtils.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Net.Http.Headers;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Couchbase.Lite.Auth
{
    internal static class AuthUtils
    {
        public static IDictionary<string, string> ParseAuthHeader(AuthenticationHeaderValue header)
        {
            if(header == null) {
                return null;
            }

            var retVal = new Dictionary<string, string>();
            var raw = header.Parameter;
            var regex = new Regex("(\\w+)=((\\w+)|\"([^\"]+))");
            var matches = regex.Matches(raw);
            if(matches.Count > 0) { 
                var groups = matches[0].Groups;
                var key = groups[1].Value;
                var k = groups[3];
                if(!k.Success) {
                    k = groups[4];
                }

                retVal[key] = k.Value;
                retVal["Scheme"] = header.Scheme;
            }

            retVal["WWW-Authenticate"] = header.ToString();
            return retVal;
        }
    }
}

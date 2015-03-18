//
//  CouchbaseLiteRouter.cs
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
using System.Collections.Generic;
using Couchbase.Lite.Util;
using System.Collections.Specialized;

namespace Couchbase.Lite.PeerToPeer
{
    internal delegate CouchbaseLiteResponse RestMethod(HttpListenerContext context);

    internal abstract class CouchbaseLiteRouter
    {
        private static readonly Dictionary<string, RestMethod> _Get = 
            new Dictionary<string, RestMethod> {
            { "", ServerMethods.Greeting },
            { "_all_dbs", ServerMethods.GetAllDbs },
            { "_session", ServerMethods.GetSession },
            { "_uuids", ServerMethods.GetUUIDs }
        };

        private static readonly Dictionary<string, RestMethod> _Post =
            new Dictionary<string, RestMethod> {
            { "_replicate", ServerMethods.ManageReplicationSession }
        };

        public static void HandleContext(HttpListenerContext context)
        {
            var request = context.Request;
            var url = request.Url.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (url.Length == 0) {
                url = new[] { string.Empty };
            }

            var method = request.HttpMethod;

            RestMethod logic = null;
            bool gotLogic = false;
            if (method.Equals("GET") || method.Equals("HEAD")) {
                gotLogic = _Get.TryGetValue(url[url.Length - 1], out logic);
                if (!gotLogic) {
                    logic = DefaultGetMethod(url.Length);
                    gotLogic = logic != null;
                }
            } else if (method.Equals("POST")) {
                gotLogic = _Post.TryGetValue(url[url.Length - 1], out logic);
            } else if (method.Equals("PUT")) {

            } else if (method.Equals("DELETE")) {
                if (!gotLogic) {
                    logic = DefaultDeleteMethod(url.Length);
                    gotLogic = logic != null;
                }
            }

            CouchbaseLiteResponse res = null;
            if (gotLogic) {
                res = logic(context);
            } else if (url.Length == 1) {
                
            } else {
                res = new CouchbaseLiteResponse();
                res.InternalStatus = StatusCode.NotFound;
            }

            res.WriteToContext(context);
            context.Response.Close();
        }

        private static RestMethod DefaultGetMethod(int urlPortionCount)
        {
            if (urlPortionCount == 1) {
                return DatabaseMethods.GetDatabaseConfiguration;
            }

            return null;
        }

        private static RestMethod DefaultDeleteMethod(int urlPortionCount)
        {
            if (urlPortionCount == 1) {
                return DatabaseMethods.DeleteDatabaseConfiguration;
            }

            return null;
        }
    }
}


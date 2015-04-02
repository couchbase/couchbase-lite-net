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
    internal delegate ICouchbaseResponseState RestMethod(HttpListenerContext context);

    internal static class CouchbaseLiteRouter
    {
        private const string TAG = "CouchbaseLiteRouter";
        private static readonly List<ICouchbaseResponseState> _UnfinishedResponses = new List<ICouchbaseResponseState>();

        private static readonly RouteCollection _Get = 
            new RouteCollection(new Dictionary<string, RestMethod> {
                { "/", ServerMethods.Greeting },
                { "/_all_dbs", ServerMethods.GetAllDbs },
                { "/_session", ServerMethods.GetSession },
                { "/_uuids", ServerMethods.GetUUIDs },
                { "/*", DatabaseMethods.GetConfiguration },
                { "/*/_all_docs", DatabaseMethods.GetAllDocuments },
                { "/*/_changes", DatabaseMethods.GetChanges },
                { "/*/*", DocumentMethods.GetDocument },
                { "/*/_local/*", DocumentMethods.GetDocument },
                { "/*/*/*", DocumentMethods.GetAttachment }
            });

        private static readonly RouteCollection _Post =
            new RouteCollection(new Dictionary<string, RestMethod> {
                { "/_replicate", ServerMethods.ManageReplicationSession },
                { "/*/_all_docs", DatabaseMethods.GetAllSpecifiedDocuments },
                { "/*/_bulk_docs", DatabaseMethods.ProcessDocumentChangeOperations },
                { "/*/_compact", DatabaseMethods.Compact },
                { "/*/_purge", DatabaseMethods.Purge },
                { "/*/_temp_view", DatabaseMethods.ExecuteTemporaryViewFunction },
                { "/*", DocumentMethods.CreateDocument }, //CouchDB does not have an equivalent for POST to _local
            });

        private static readonly RouteCollection _Put =
            new RouteCollection(new Dictionary<string, RestMethod> {
                { "/*", DatabaseMethods.UpdateConfiguration },
                { "/*/*", DocumentMethods.UpdateDocument },
                { "/*/_local/*", DocumentMethods.UpdateDocument },
                { "/*/*/*", DocumentMethods.UpdateAttachment }
            });

        private static readonly RouteCollection _Delete =
            new RouteCollection(new Dictionary<string, RestMethod> {
                { "/*", DatabaseMethods.DeleteConfiguration },
                { "/*/*", DocumentMethods.DeleteDocument },
                { "/*/_local/*", DocumentMethods.DeleteDocument },
                { "/*/*/*", DocumentMethods.DeleteAttachment }
            });

        public static void HandleContext(HttpListenerContext context)
        {
            var request = context.Request;
            var method = request.HttpMethod;

            RestMethod logic = null;
            if (method.Equals("GET") || method.Equals("HEAD")) {
                logic = _Get.LogicForRequest(request);
            } else if (method.Equals("POST")) {
                logic = _Post.LogicForRequest(request);
            } else if (method.Equals("PUT")) {
                logic = _Put.LogicForRequest(request);
            } else if (method.Equals("DELETE")) {
                logic = _Delete.LogicForRequest(request);
            }

            ICouchbaseResponseState responseState = null;
            try {
                responseState = logic(context);
            } catch(Exception e) {
                Log.E(TAG, "Exception in routing logic", e);
                responseState = new CouchbaseLiteResponse(context) { InternalStatus = StatusCode.Exception }.AsDefaultState();
            }

            CouchbaseLiteResponse responseObject = responseState.Response;
            if (!responseState.IsAsync) {
                try {
                    responseObject.WriteHeaders();
                    responseObject.WriteToContext();
                } catch(Exception e) {
                    Log.E(TAG, "Exception writing response", e);
                    responseState = new CouchbaseLiteResponse(context) { InternalStatus = StatusCode.Exception }.AsDefaultState();
                }
            } else {
                _UnfinishedResponses.Add(responseState);
            }
        }

        public static void ResponseFinished(ICouchbaseResponseState responseState)
        {
            _UnfinishedResponses.Remove(responseState);
        }
            
    }
}


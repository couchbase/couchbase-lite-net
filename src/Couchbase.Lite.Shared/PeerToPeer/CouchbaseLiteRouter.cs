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
using System.Collections.Generic;
using System.Net;

using Couchbase.Lite.Util;
using System.Net.Http;

namespace Couchbase.Lite.PeerToPeer
{
    internal delegate ICouchbaseResponseState RestMethod(ICouchbaseListenerContext context);

    internal partial class CouchbaseLiteRouter
    {
        private const string TAG = "CouchbaseLiteRouter";
        private static readonly List<ICouchbaseResponseState> _UnfinishedResponses = new List<ICouchbaseResponseState>();

        private static readonly RestMethod NOT_ALLOWED = 
            context => new CouchbaseLiteResponse(context) { InternalStatus = StatusCode.MethodNotAllowed }.AsDefaultState();

        private readonly Manager _manager;

        private static readonly RouteCollection _Get = 
            new RouteCollection(new Dictionary<string, RestMethod> {
                { "/", ServerMethods.Greeting },
                { "/_all_dbs", ServerMethods.GetAllDbs },
                { "/_session", ServerMethods.GetSession },
                { "/_uuids", ServerMethods.GetUUIDs },
                { "/{[^_].*}", DatabaseMethods.GetConfiguration },
                { "/{[^_].*}/_all_docs", DatabaseMethods.GetAllDocuments },
                { "/{[^_].*}/_changes", DatabaseMethods.GetChanges },
                { "/{[^_].*}/*", DocumentMethods.GetDocument },
                { "/{[^_].*}/_local/*", DocumentMethods.GetDocument },
                { "/{[^_].*}/*/**", DocumentMethods.GetAttachment },
                { "/{[^_].*}/_design/*/_view/*", ViewMethods.GetDesignView }
            });

        private static readonly RouteCollection _Post =
            new RouteCollection(new Dictionary<string, RestMethod> {
                { "/_replicate", ServerMethods.ManageReplicationSession },
                { "/{[^_].*}/_all_docs", DatabaseMethods.GetAllSpecifiedDocuments },
                { "/{[^_].*}/_bulk_docs", DatabaseMethods.ProcessDocumentChangeOperations },
                { "/{[^_].*}/_compact", DatabaseMethods.Compact },
                { "/{[^_].*}/_purge", DatabaseMethods.Purge },
                { "/{[^_].*}/_temp_view", DatabaseMethods.ExecuteTemporaryViewFunction },
                { "/{[^_].*}", DocumentMethods.CreateDocument }, //CouchDB does not have an equivalent for POST to _local
                { "/_facebook_token", AuthenticationMethods.RegisterFacebookToken },
                { "/_persona_assertion", AuthenticationMethods.RegisterPersonaToken }
            });

        private static readonly RouteCollection _Put =
            new RouteCollection(new Dictionary<string, RestMethod> {
                { "/{[^_].*}", DatabaseMethods.UpdateConfiguration },
                { "/{[^_].*}/*", DocumentMethods.UpdateDocument },
                { "/{[^_].*}/_local/*", DocumentMethods.UpdateDocument },
                { "/{[^_].*}/*/**", DocumentMethods.UpdateAttachment },
                { "/{[^_].*}/_design/*", DocumentMethods.UpdateDocument }
            });

        private static readonly RouteCollection _Delete =
            new RouteCollection(new Dictionary<string, RestMethod> {
                { "/{[^_].*}", DatabaseMethods.DeleteConfiguration },
                { "/{[^_].*}/*", DocumentMethods.DeleteDocument },
                { "/{[^_].*}/_local/*", DocumentMethods.DeleteDocument },
                { "/{[^_].*}/*/*", DocumentMethods.DeleteAttachment }
            });

        public CouchbaseLiteRouter(Manager manager) {
            _manager = manager;
        }

        public void HandleContext(HttpListenerContext context)
        {
            var request = context.Request;
            var wrappedContext = new CouchbaseListenerContext(context, _manager);
            var method = wrappedContext.Method;

            RestMethod logic = null;
            if (method.Equals(HttpMethod.Get) || method.Equals(HttpMethod.Head)) {
                logic = _Get.LogicForRequest(request);
            } else if (method.Equals(HttpMethod.Post)) {
                logic = _Post.LogicForRequest(request);
            } else if (method.Equals(HttpMethod.Put)) {
                logic = _Put.LogicForRequest(request);
            } else if (method.Equals(HttpMethod.Delete)) {
                logic = _Delete.LogicForRequest(request);
            } else {
                logic = NOT_ALLOWED;
            }

            ICouchbaseResponseState responseState = null;

            try {
                responseState = logic(wrappedContext);
            } catch(Exception e) {
                
                var ce = e as CouchbaseLiteException;
                if (ce != null) {
                    Log.I(TAG, "Couchbase exception in routing logic, this message can be ignored if intentional", e);
                    responseState = new CouchbaseLiteResponse(wrappedContext) { InternalStatus = ce.GetCBLStatus().GetCode() }
                        .AsDefaultState();
                } else {
                    Log.I(TAG, "Unhandled non-Couchbase exception in routing logic", e);
                    responseState = new CouchbaseLiteResponse(wrappedContext) { InternalStatus = StatusCode.Exception }.AsDefaultState();
                }
            }

            CouchbaseLiteResponse responseObject = CheckForAltMethod(wrappedContext, responseState.Response);
            if (!responseState.IsAsync) {
                try {
                    responseObject.ProcessRequestRanges();
                    responseObject.WriteHeaders();
                    responseObject.WriteToContext();
                } catch(Exception e) {
                    Log.E(TAG, "Exception writing response", e);
                    responseState = new CouchbaseLiteResponse(wrappedContext) { InternalStatus = StatusCode.Exception }.AsDefaultState();
                }
            } else {
                _UnfinishedResponses.Add(responseState);
            }
        }

        public static void ResponseFinished(ICouchbaseResponseState responseState)
        {
            _UnfinishedResponses.Remove(responseState);
        }
            
        private static CouchbaseLiteResponse CheckForAltMethod(ICouchbaseListenerContext context, CouchbaseLiteResponse response)
        {
            if (response.Status != RouteCollection.EndpointNotFoundStatus) {
                return response;
            }
                
            HttpListenerRequest request = context.HttpContext.Request;
            bool hasAltMethod = _Delete.HasLogicForRequest(request) || _Get.HasLogicForRequest(request)
                                || _Post.HasLogicForRequest(request) || _Put.HasLogicForRequest(request);

            if (hasAltMethod) {
                return new CouchbaseLiteResponse(context) { InternalStatus = StatusCode.MethodNotAllowed };
            }

            return new CouchbaseLiteResponse(context) { InternalStatus = StatusCode.NotFound };
        }
    }
}


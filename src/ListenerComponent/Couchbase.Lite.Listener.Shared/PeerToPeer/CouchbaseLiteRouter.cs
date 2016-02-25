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

using Couchbase.Lite.Util;

namespace Couchbase.Lite.Listener
{

    /// <summary>
    /// The signature for a method containing the logic for a REST endpoint
    /// </summary>
    internal delegate ICouchbaseResponseState RestMethod(ICouchbaseListenerContext context);

    /// <summary>
    /// The class that handles the routing for an incoming request via Couchbase Lite P2P
    /// </summary>
    public sealed class CouchbaseLiteRouter
    {

        #region Constants

        private const string TAG = "CouchbaseLiteRouter";

        private static readonly RouteCollection _Get = 
            new RouteCollection("GET", new Dictionary<string, RestMethod> {
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
            new RouteCollection("POST", new Dictionary<string, RestMethod> {
                { "/_replicate", ServerMethods.ManageReplicationSession },
                { "/{[^_].*}/_revs_diff", DatabaseMethods.RevsDiff },
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
            new RouteCollection("PUT", new Dictionary<string, RestMethod> {
                { "/{[^_].*}", DatabaseMethods.UpdateConfiguration },
                { "/{[^_].*}/*", DocumentMethods.UpdateDocument },
                { "/{[^_].*}/_local/*", DocumentMethods.UpdateDocument },
                { "/{[^_].*}/*/**", DocumentMethods.UpdateAttachment },
                { "/{[^_].*}/_design/*", DocumentMethods.UpdateDocument }
            });

        private static readonly RouteCollection _Delete =
            new RouteCollection("DELETE", new Dictionary<string, RestMethod> {
                { "/{[^_].*}", DatabaseMethods.DeleteConfiguration },
                { "/{[^_].*}/*", DocumentMethods.DeleteDocument },
                { "/{[^_].*}/_local/*", DocumentMethods.DeleteDocument },
                { "/{[^_].*}/*/*", DocumentMethods.DeleteAttachment }
            });

        #endregion

        #region Variables

        // A storage location for continuous / async responses so they don't get GC'd
        private static readonly List<ICouchbaseResponseState> _UnfinishedResponses = new List<ICouchbaseResponseState>();

        // The default response when an endpoint is requested via an incorrect method type (i.e. using POST on
        // a method that only has GET)
        private static readonly RestMethod NOT_ALLOWED = 
            context => context.CreateResponse(StatusCode.MethodNotAllowed).AsDefaultState();

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the logic for verifying access to endpoints (e.g. don't allow endpoints
        /// that modify state on a read-only connection)
        /// </summary>
        public Func<string, string, Status> OnAccessCheck { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// The entry point for routing a request received by an CouchbaseLiteServiceListener
        /// </summary>
        /// <param name="context">The context containing information about the
        /// request</param>
        public void HandleRequest(ICouchbaseListenerContext context)
        {
            Log.To.Router.I(TAG, "Processing {0} request to {1}", context.Method, context.RequestUrl.AbsoluteUri);
            var method = context.Method;

            if (OnAccessCheck != null) {
                Status result = null;
                try {
                    result = OnAccessCheck(method, context.RequestUrl.AbsolutePath);
                } catch(Exception e) {
                    result = new Status(StatusCode.Exception);
                    Log.To.Router.E(TAG, "Unhandled non-Couchbase exception in OnAccessCheck", e);
                }

                if (result.IsError) {
                    var r = context.CreateResponse(result.Code);
                    ProcessResponse(context, r.AsDefaultState());
                    return;
                }
            }

            RestMethod logic = null;
            if (method.Equals("GET") || method.Equals("HEAD")) {
                logic = _Get.LogicForRequest(context.RequestUrl);
            } else if (method.Equals("POST")) {
                logic = _Post.LogicForRequest(context.RequestUrl);
            } else if (method.Equals("PUT")) {
                logic = _Put.LogicForRequest(context.RequestUrl);
            } else if (method.Equals("DELETE")) {
                logic = _Delete.LogicForRequest(context.RequestUrl);
            } else {
                logic = NOT_ALLOWED; // Shouldn't happen
            }

            ICouchbaseResponseState responseState = null;

            try {
                responseState = logic(context);
            } catch(Exception e) {
                var ce = e as CouchbaseLiteException;
                if (ce != null) {
                    // This is in place so that a response can be written simply by throwing a couchbase lite exception
                    // in the routing logic
                    Log.To.Router.I(TAG, "Couchbase exception in routing logic, this message can be ignored if intentional", e);
                    responseState = context.CreateResponse(ce.CBLStatus.Code).AsDefaultState();
                } else {
                    Log.To.Router.E(TAG, "Unhandled non-Couchbase exception in routing logic", e);
                    responseState = context.CreateResponse(StatusCode.Exception).AsDefaultState();
                }
            }

            ProcessResponse(context, responseState);
        }

        /// <summary>
        /// Inform the router that an async / continuous response has completed and can be
        /// finalized.
        /// </summary>
        /// <param name="responseState">The response that finished</param>
        public static void ResponseFinished(ICouchbaseResponseState responseState)
        {
            _UnfinishedResponses.Remove(responseState);
        }

        #endregion

        #region Private Methods

        // Attempt to write the response over the wire to the client
        private static void ProcessResponse(ICouchbaseListenerContext context, ICouchbaseResponseState responseState)
        {
            CouchbaseLiteResponse responseObject = CheckForAltMethod(context, responseState.Response);
            if (!responseState.IsAsync) {
                try {
                    responseObject.ProcessRequestRanges();
                    responseObject.WriteHeaders();
                    responseObject.WriteToContext();
                } catch(Exception e) {
                    Log.To.Router.E(TAG, "Exception writing response", e);
                    responseState = context.CreateResponse(StatusCode.Exception).AsDefaultState();
                }
            } else {
                _UnfinishedResponses.Add(responseState);
            }
        }
            
        // Check for an incorrect request method on a request
        private static CouchbaseLiteResponse CheckForAltMethod(ICouchbaseListenerContext context, CouchbaseLiteResponse response)
        {
            if (response.Status != RouteCollection.EndpointNotFoundStatus) {
                return response;
            }
                
            Log.To.Router.I(TAG, "{0} method not found for endpoint {1}, searching for alternate...",
                context.Method, context.RequestUrl.PathAndQuery);
            var request = context.RequestUrl;
            bool hasAltMethod = _Delete.HasLogicForRequest(request) || _Get.HasLogicForRequest(request)
                                || _Post.HasLogicForRequest(request) || _Put.HasLogicForRequest(request);

            if (hasAltMethod) {
                Log.To.Router.I(TAG, "Suitable method found; returning 406");
                return context.CreateResponse(StatusCode.MethodNotAllowed);
            }

            Log.To.Router.I(TAG, "No suitable method found; returning 404", context.RequestUrl.PathAndQuery);
            return context.CreateResponse(StatusCode.NotFound);
        }

        #endregion

    }
}


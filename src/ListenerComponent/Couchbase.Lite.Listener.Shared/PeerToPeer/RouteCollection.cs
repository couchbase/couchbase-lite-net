//
//  RouteCollection.cs
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
    /// A collection object for storing REST endpoints
    /// </summary>
    internal class RouteCollection
    {

        #region Constants

        private static readonly string Tag = typeof(RouteCollection).Name;
        public const int EndpointNotFoundStatus = -2; // To distinguish a legitimate 404 response from an actual endpoint not being found
        private static readonly RestMethod NOT_FOUND = 
            context => {
            var retVal = context.CreateResponse();
            retVal.Status = EndpointNotFoundStatus;
            return retVal.AsDefaultState();
        }; 

        #endregion

        #region Variables

        private readonly RouteTree _routeTree = new RouteTree();
        private readonly string _name;

        #endregion

        #region Constructors

        public RouteCollection(string name, IDictionary<string, RestMethod> map)
        {
            _name = name;
            AddRoutes(map);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Registers the given method for the given endpoint
        /// </summary>
        /// <param name="endpoint">The endpoint to register with the logic</param>
        /// <param name="logic">The logic to use for the endpoint</param>
        public void AddRoute(string endpoint, RestMethod logic)
        {
            var branch = _routeTree.Trunk;
            var components = endpoint.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var component in components) {
                branch = branch.GetChild(component, true);
            }

            branch.Logic = logic;
        }

        /// <summary>
        /// Add multiple endpoints to the collection
        /// </summary>
        /// <param name="map">A dictionary containing URL strings and their logic</param>
        public void AddRoutes(IDictionary<string, RestMethod> map)
        {
            foreach (var entry in map) {
                AddRoute(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Retrieve the logic for the given request
        /// </summary>
        /// <returns>The logic for the given request (returns a status of -2 if there is none)</returns>
        /// <param name="request">The incoming request</param>
        public RestMethod LogicForRequest(Uri request)
        {
            Log.To.Router.V(Tag, "Begin searching for logic for {0} {1}...", _name, request.PathAndQuery);
            var branch = _routeTree.Trunk;
            //MS .NET will automatically unescape the string so we need to be careful here
            var tmp = request.AbsolutePath.Split('?')[0];
            string[] components = tmp.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var component in components) {
                Log.To.Router.V(Tag, "Next component in path is {0}...", component);
                var nextBranch = branch.GetChild(component, false);
                if (nextBranch == null) {
                    Log.To.Router.V(Tag, "Intermediate component not found");
                    return NOT_FOUND;
                }
                branch = nextBranch;
            }
                
            if (branch.Logic == null) {
                Log.To.Router.V(Tag, "No logic for endpoint");
                return NOT_FOUND;
            }

            return branch.Logic;
        }

        /// <summary>
        /// Checks to see if the collection has logic for the given request
        /// </summary>
        /// <returns><c>true</c> if this collection has logic for request the specified request; otherwise, <c>false</c>.</returns>
        /// <param name="request">The incoming request</param>
        public bool HasLogicForRequest(Uri request)
        {
            var logic = LogicForRequest(request);
            var retVal = logic != NOT_FOUND;
            Log.To.Router.V(Tag, "{0} method {1}found for {2}", _name, retVal ? String.Empty : "not ", request.PathAndQuery);
            return retVal;
        }

        #endregion

    }
}


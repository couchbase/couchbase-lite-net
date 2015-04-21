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
using System.Net;

namespace Couchbase.Lite.Listener
{
    internal class RouteCollection
    {
        public const int EndpointNotFoundStatus = -2;

        private readonly RouteTree _routeTree = new RouteTree();
        private static readonly RestMethod NOT_FOUND = 
            context => new CouchbaseLiteResponse(context) { Status = EndpointNotFoundStatus }.AsDefaultState();

        public RouteCollection(IDictionary<string, RestMethod> map)
        {
            AddRoutes(map);
        }

        public void AddRoute(string endpoint, RestMethod logic)
        {
            var branch = _routeTree.Trunk;
            var components = endpoint.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var component in components) {
                branch = branch.GetChild(component, true);
            }

            branch.Logic = logic;
        }

        public void AddRoutes(IDictionary<string, RestMethod> map)
        {
            foreach (var entry in map) {
                AddRoute(entry.Key, entry.Value);
            }
        }

        public RestMethod LogicForRequest(HttpListenerRequest request)
        {
            var branch = _routeTree.Trunk;
            //MS .NET will automatically unescape the string so we need to be careful here
            var tmp = request.RawUrl.Split('?')[0];
            string[] components = tmp.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var component in components) {
                var nextBranch = branch.GetChild(component, false);
                if (nextBranch == null) {
                    return NOT_FOUND;
                }
                branch = nextBranch;
            }
                
            return branch.Logic ?? NOT_FOUND;
        }

        public bool HasLogicForRequest(HttpListenerRequest request)
        {
            var branch = _routeTree.Trunk;
            var components = request.Url.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var component in components) {
                var nextBranch = branch.GetChild(component, false);
                if (nextBranch == null) {
                    return false;
                }
                branch = nextBranch;
            }

            return branch.Logic != null;
        }
    }
}


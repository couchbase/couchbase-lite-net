//
//  RouteTree.cs
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
using System.Text.RegularExpressions;

namespace Couchbase.Lite.PeerToPeer
{
    internal interface IRouteTreeBranch
    {
        IRouteTreeBranch Parent { get; }

        RestMethod Logic { get; set; }

        IRouteTreeBranch GetChild(string endpointName, bool create);

        IRouteTreeBranch AddBranch(string endpoint, RestMethod logic);
    }

    internal sealed class RouteTree
    {
        private sealed class Branch : IRouteTreeBranch
        {
            private readonly Dictionary<string, IRouteTreeBranch> _literalBranches = new Dictionary<string, IRouteTreeBranch>();
            private readonly Dictionary<Regex, IRouteTreeBranch> _regexBranches = new Dictionary<Regex, IRouteTreeBranch>();
            private IRouteTreeBranch _wildcardBranch = null;

            public IRouteTreeBranch Parent { get; private set; }

            public RestMethod Logic { get; set; }

            public Branch(Branch parent)
            {
                Parent = parent;
            }

            public IRouteTreeBranch GetChild(string endpointName, bool create)
            {
                IRouteTreeBranch retVal = null;
                if (_literalBranches.TryGetValue(endpointName, out retVal)) {
                    return retVal;
                }

                foreach (var pair in _regexBranches) {
                    if (pair.Key.IsMatch(endpointName)) {
                        return pair.Value;
                    }
                }

                if (!create || (_wildcardBranch != null && endpointName.Equals("*"))) {
                    return _wildcardBranch;
                }

                return AddBranch(endpointName, null);
            }

            public IRouteTreeBranch AddBranch(string endpoint, RestMethod logic)
            {
                var branch = new Branch(this) { Logic = logic };
                if (endpoint.Equals("*")) {
                    _wildcardBranch = branch;
                } else if (endpoint.StartsWith("{")) {
                    endpoint = String.Format("^{0}$", endpoint.Trim('{', '}'));
                    _regexBranches[new Regex(endpoint)] = branch;
                } else {
                    _literalBranches[endpoint] = branch;
                }

                return branch;
            }
        }

        public IRouteTreeBranch Trunk { get; private set; }

        public RouteTree()
        {
            Trunk = new Branch(null);
        }
    }
}


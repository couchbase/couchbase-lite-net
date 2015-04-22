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

namespace Couchbase.Lite.Listener
{

    /// <summary>
    /// A node inside of a <c>RouteTree</c> object that holds an endpoint and
    /// its logic, as well as the node's parent and children <seealso cref="RouteTree"/>
    /// </summary>
    /// <remarks>
    /// There are four types of endpoints that can be added:
    /// 
    /// 1) Literal endpoints (e.g. /endpoint)
    /// 2) Regex endpoints (e.g. /{endpoint(s)?})
    /// 3) Wildcard endpoints (e.g. /endpoint/*)
    /// 4) Greedy endpoints (e.g. /endpoint/**)
    /// 
    /// Endpoints are evaluated in that order so for example /endpoint/foo has higher precedence then /endpoint/*
    /// 
    /// Greedy endpoints will capture everything until the end of the URL, so /endpoint/** will not only capture
    /// /endpoint/foo but /endpoint/foo/bar as well
    /// </remarks>
    internal interface IRouteTreeBranch
    {

        /// <summary>
        /// The node's parent in the tree
        /// </summary>
        IRouteTreeBranch Parent { get; }

        /// <summary>
        /// The logic contained in the node
        /// </summary>
        RestMethod Logic { get; set; }

        /// <summary>
        /// Search recursively for a child that matches the given endpoint, and optionally
        /// creates it
        /// </summary>
        /// <returns>The branch that corresponds to the endpoint</returns>
        /// <param name="endpointName">The name of the endpoint to search for</param>
        /// <param name="create">If set to <c>true</c> create the endpoint</param>
        IRouteTreeBranch GetChild(string endpointName, bool create);

        /// <summary>
        /// Adds a child branch to this node
        /// </summary>
        /// <returns>The added branch</returns>
        /// <param name="endpoint">The endpoint identifying the branch</param>
        /// <param name="logic">The logic for the endpoint</param>
        IRouteTreeBranch AddBranch(string endpoint, RestMethod logic);

    }

    /// <summary>
    /// A data structure for storing REST endpoints and corresponding logic
    /// </summary>
    internal sealed class RouteTree
    {

        // Branch for "**" endpoints
        private sealed class GreedyBranch : IRouteTreeBranch
        {

            #region Properties

            // IRouteTreeBranch
            public IRouteTreeBranch Parent { get; private set; }

            // IRouteTreeBranch
            public RestMethod Logic { get; set; }

            #endregion

            #region Constructors

            public GreedyBranch(IRouteTreeBranch parent)
            {
                Parent = parent;
            }

            #endregion

            #region IRouteTreeBranch

            public IRouteTreeBranch GetChild(string endpointName, bool create)
            {
                return this;
            }
                
            public IRouteTreeBranch AddBranch(string endpoint, RestMethod logic)
            {
                throw new NotSupportedException(); // It doesn't make sense to add a child to a greedy branch
            }

            #endregion

        }

        // Branch for all other endpoints
        private sealed class Branch : IRouteTreeBranch
        {

            #region Variables 

            private readonly Dictionary<string, IRouteTreeBranch> _literalBranches = new Dictionary<string, IRouteTreeBranch>();
            private readonly Dictionary<Regex, IRouteTreeBranch> _regexBranches = new Dictionary<Regex, IRouteTreeBranch>();
            private IRouteTreeBranch _wildcardBranch = null;
            private IRouteTreeBranch _greedyBranch = null;

            #endregion

            #region Properties

            // IRouteTreeBranch
            public IRouteTreeBranch Parent { get; private set; }

            // IRouteTreeBranch
            public RestMethod Logic { get; set; }

            #endregion

            #region Constructors

            public Branch(Branch parent)
            {
                Parent = parent;
            }

            #endregion

            #region IRouteTreeBranch

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

                if ((_wildcardBranch != null && endpointName.Equals("*"))) {
                    return _wildcardBranch;
                }

                //NOTE.JHB: Fragile and will break if any other branch fails to return
                //but so far it is good enough
                if (_greedyBranch != null && endpointName.Equals("**")) {
                    return _greedyBranch;
                }

                if (!create) {
                    return _wildcardBranch ?? _greedyBranch;
                }

                return AddBranch(endpointName, null);
            }

            public IRouteTreeBranch AddBranch(string endpoint, RestMethod logic)
            {
                IRouteTreeBranch branch = new Branch(this) { Logic = logic };
                if (endpoint.Equals("*")) {
                    _wildcardBranch = branch;
                } else if (endpoint.Equals("**")) {
                    _greedyBranch = new GreedyBranch(this) { Logic = logic };
                    branch = _greedyBranch;
                } else if (endpoint.StartsWith("{")) {
                    endpoint = String.Format("^{0}$", endpoint.Trim('{', '}'));
                    _regexBranches[new Regex(endpoint)] = branch;
                } else {
                    _literalBranches[endpoint] = branch;
                }

                return branch;
            }

            #endregion

        }

        #region Properties

        /// <summary>
        /// Gets the root node of this structure
        /// </summary>
        public IRouteTreeBranch Trunk { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        public RouteTree()
        {
            Trunk = new Branch(null);
        }

        #endregion

    }
}


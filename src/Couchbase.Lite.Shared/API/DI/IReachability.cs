// 
// IReachability.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using Couchbase.Lite.Sync;

namespace Couchbase.Lite.DI
{
    /// <summary>
    /// An interface for describing whether a given URL is reachable via
    /// network connection or not.
    /// </summary>
    public interface IReachability
    {
        #region Variables

        /// <summary>
        /// Fired when the status of connectivity changes
        /// </summary>
        event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;

        #endregion

        #region Properties

        /// <summary>
        /// The URL to track connectivity to
        /// </summary>
        Uri? Url { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Start monitoring for changes in network status
        /// </summary>
        void Start();

        /// <summary>
        /// Stop monitoring for changes in network status
        /// </summary>
        void Stop();

        #endregion
    }
}

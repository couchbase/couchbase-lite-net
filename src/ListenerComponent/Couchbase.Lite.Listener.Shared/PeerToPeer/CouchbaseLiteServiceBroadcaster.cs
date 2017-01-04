//
//  Broadcaster.cs
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

using Mono.Zeroconf;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Listener
{
   
    /// <summary>
    /// A class designed to broadcast the availability of a Couchbase Lite P2P
    /// service.
    /// </summary>
    public sealed class CouchbaseLiteServiceBroadcaster : IDisposable
    {
        #region Constants

        private const string TAG = "CouchbaseLiteServiceBroadcaster";

        #endregion

        #region Members

        private volatile bool _running;
        private readonly IRegisterService _registerService;

        #endregion

        #region Properties

        /// <summary>
        /// The name of the service being broadcast
        /// </summary>
        public string Name
        {
            get { return _registerService.Name; }
            set { _registerService.Name = value; }
        }

        /// <summary>
        /// The type of service being broadcast (e.g. _http._tcp)
        /// </summary>
        public string Type
        { 
            get { return _registerService.RegType; }
            set { _registerService.RegType = value; }
        }

        /// <summary>
        /// The connection port to use for connecting to the service
        /// </summary>
        /// <value>The port.</value>
        public ushort Port
        {
            get { return _registerService.Port; }
            set { _registerService.Port = value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new broadcaster class with the given IRegisterService
        /// (or Bonjour if null)
        /// </summary>
        /// <param name="registerService">The service that will perform the broadcast</param>
        /// <param name = "port">The port to advertise the service as being available on (i.e.
        /// the port that the listener is using)</param>
        public CouchbaseLiteServiceBroadcaster(IRegisterService registerService, ushort port)
        {
            if (registerService == null) {
                Log.To.Discovery.E(TAG, "registerService null in ctor, throwing...");
                throw new ArgumentNullException("registerService");
            }

            if (registerService.Name == null) {
                registerService.Name = "CouchbaseLite_" + Environment.MachineName;   
            }

            if (registerService.RegType == null) {
                registerService.RegType = "_http._tcp";
            }

            _registerService = registerService;
            _registerService.Port = port;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the broadcaster
        /// </summary>
        public void Start()
        {
            if (_running) {
                return;
            }

            _running = true;
            _registerService.Register();
            Log.To.Discovery.I(TAG, "Started {0}", this);
        }

        /// <summary>
        /// Stops the broadcaster
        /// </summary>
        public void Stop()
        {
            if (!_running) {
                return;
            }

            _running = false;
            _registerService.Unregister();
            Log.To.Discovery.I(TAG, "Stopped {0}", this);
        }

        #endregion

        #region Overrides
#pragma warning disable 1591

        public override string ToString()
        {
            return string.Format("CouchbaseLiteServiceBroadcaster[Name={0}, Type={1}, Port={2}]", Name, Type, Port);
        }

        #endregion

        #region IDisposable
     
        public void Dispose()
        {
            if (!_running) {
                return;
            }

            _running = false;
            _registerService.Dispose();
        }

#pragma warning restore 1591
        #endregion
    }
}


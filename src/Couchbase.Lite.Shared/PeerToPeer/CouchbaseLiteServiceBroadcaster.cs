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

using Mono.Zeroconf.Providers.Bonjour;
using Mono.Zeroconf;

#if ANDROID
using Android.App;
#endif

namespace Couchbase.Lite.PeerToPeer
{
   
    /// <summary>
    /// A class designed to broadcast the availability of a Couchbase Lite P2P
    /// service.
    /// </summary>
    public sealed class CouchbaseLiteServiceBroadcaster : IDisposable
    {

        #region Members

        private const string TAG = "CouchbaseLiteServiceBroadcaster";
        private volatile bool _running;
        private IRegisterService _registerService;

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
            get { return _registerService.UPort; }
            set { _registerService.UPort = value; }
        }

        #endregion

        #region Constructors

        #if ANDROID
        /// <summary>
        /// This is needed to start the /system/bin/mdnsd service on Android
        /// (can't find another way to start it)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoOptimization)]
        static CouchbaseLiteServiceBroadcaster() {
            Application.Context.GetSystemService("servicediscovery");
        }
        #endif

        /// <summary>
        /// Creates a new broadcaster class with the given IRegisterService
        /// (or Bonjour if null)
        /// </summary>
        /// <param name="registerService">The service that will perform the broadcast</param>
        public CouchbaseLiteServiceBroadcaster(IRegisterService registerService)
        {
            _registerService = registerService ?? new RegisterService() {
                Name = "CouchbaseLite_" + Environment.MachineName,
                RegType = "_http._tcp."
            };

            _registerService.UPort = 4984;
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

        #endregion
    }
}


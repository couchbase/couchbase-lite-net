//
//  Listener.cs
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
using System.Linq;

using Mono.Zeroconf;

namespace Couchbase.Lite.Listener
{

    /// <summary>
    /// A class designed to browse for services advertised by CouchbaseLiteServiceBroadcaster
    /// </summary>
    public sealed class CouchbaseLiteServiceBrowser : IDisposable
    {

        #region Members 

        private readonly IServiceBrowser _browser;
        private bool _running = false;

        #endregion

        #region Properties

        /// <summary>
        /// The type of services to browse for (e.g. _http._tcp)
        /// </summary>
        public string Type { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// An event raised when a new service is discovered and resolved
        /// </summary>
        public event ServiceResolvedEventHandler ServiceResolved
        {
            add { _serviceResolved = (ServiceResolvedEventHandler)Delegate.Combine(_serviceResolved, value); }
            remove { _serviceResolved = (ServiceResolvedEventHandler)Delegate.Remove(_serviceResolved, value); }
        }
        private event ServiceResolvedEventHandler _serviceResolved;

        /// <summary>
        /// An event raised when an existing service is destroyed
        /// </summary>
        public event ServiceBrowseEventHandler ServiceRemoved {
            add { _browser.ServiceRemoved += value; }
            remove { _browser.ServiceRemoved -= value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new browser with the specified browsing service
        /// (or Bonjour if null is passed)
        /// </summary>
        /// <param name="browser">The service that will perform the browsing</param>
        public CouchbaseLiteServiceBrowser(IServiceBrowser browser)
        {
            if (browser == null) {
                throw new ArgumentNullException("browser");
            }

            _browser = browser;
            _browser.ServiceAdded += (o, args) =>
            {
                args.Service.Resolved += (_, __) => {
                    if(_serviceResolved != null) {
                        _serviceResolved(this, new ServiceResolvedEventArgs(args.Service));
                    }
                };

                args.Service.Resolve();
            };

            Type = "_http._tcp";
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the service with the specified name, or null if the service has not been
        /// discovered or is not resolved
        /// </summary>
        /// <returns>The service with the specified name</returns>
        /// <param name="name">The name of the service to retrived</param>
        public IResolvableService GetService(string name) 
        {
            return _browser.FirstOrDefault(x => x.IsResolved && x.Name.Equals(name));
        }

        /// <summary>
        /// Gets all the currently resolved services
        /// </summary>
        /// <returns>The services which are currently resolved</returns>
        public IEnumerable<IResolvableService> GetServices()
        {
            return _browser.Where(x => x.IsResolved);
        }

        /// <summary>
        /// Starts the browser
        /// </summary>
        public void Start()
        {
            if (_running) {
                return;
            }

            _running = true;
            _browser.Browse(0, AddressProtocol.IPv4, Type, "local");
        }

        /// <summary>
        /// Stops the browser
        /// </summary>
        public void Stop()
        {
            if (!_running) {
                return;
            }

            _running = false;
            _browser.Stop();
        }

        #endregion

        #region IDisposable

        public void Dispose() {
            if (!_running) {
                return;
            }

            _running = false;
            _browser.Dispose();
        }

        #endregion

    }
}


//
//  CouchbaseLiteServiceListener.cs
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

namespace Couchbase.Lite.Listener
{

    /// <summary>
    /// An abstract base class for Listening for a Couchbase Lite P2P connection
    /// </summary>
    public abstract class CouchbaseLiteServiceListener : IDisposable
    {

        #region Variables

        internal readonly CouchbaseLiteRouter _router = new CouchbaseLiteRouter();
        private bool _disposed;

        #endregion

        #region Properties

        /// <summary>
        /// Whether or not this listener is operating in read-only mode (i.e. no changes to databases
        /// are permitted)
        /// </summary>
        public bool ReadOnly {
            get {
                return _readOnly;
            }
            set {
                if (value) {
                    _router.OnAccessCheck = (method, endpoint) =>
                    {
                        if(method.Equals("HEAD") || method.Equals("GET")) {
                            return new Status(StatusCode.Ok);
                        } 
                        if(method.Equals("POST") && (endpoint.EndsWith("_all_docs") || endpoint.EndsWith("_revs_diff"))) {
                            return new Status(StatusCode.Ok);
                        }

                        return new Status(StatusCode.Forbidden);
                    };
                } else {
                    _router.OnAccessCheck = null;
                }
                _readOnly = value;
            }
        }
        private bool _readOnly;

        #endregion

        #region Public Methods

        /// <summary>
        /// Start listening and processing requests
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Stop listening and processing requests, but handle
        /// the currently received ones
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// Stop listening and processing requests immediately
        /// </summary>
        public abstract void Abort();

        #endregion

        #region Protected Methods

        /// <summary>
        /// Used by subclasses to dispose resources
        /// </summary>
        protected virtual void DisposeInternal() {}

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) {
                return;
            }

            DisposeInternal();
            _disposed = true;
        }

        #endregion
    }
}


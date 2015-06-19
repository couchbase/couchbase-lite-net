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
using System.Collections.Generic;
using Couchbase.Lite.Util;

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
        private Dictionary<string, string> _passwordMap = new Dictionary<string, string>();

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

        /// <summary>
        /// Gets a value indicating whether this <see cref="Couchbase.Lite.Listener.CouchbaseLiteServiceListener"/>
        /// requires authentication for access.
        /// </summary>
        protected bool RequiresAuth { get; private set; }

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

        /// <summary>
        /// Sets up passwords for BASIC HTTP authentication
        /// </summary>
        /// <param name="usersAndPasswords">A dictionary containing the users and their passwords</param>
        public void SetPasswords(IDictionary<string, string> usersAndPasswords)
        {
            _passwordMap.Clear();
            if (usersAndPasswords == null) {
                return;
            }

            foreach (var pair in usersAndPasswords) {
                var hashed = PasswordHash.CreateHash(pair.Value);
                _passwordMap[pair.Key] = hashed;
            }

            RequiresAuth = _passwordMap.Count > 0;
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Used by subclasses to dispose resources
        /// </summary>
        protected virtual void DisposeInternal() {}

        /// <summary>
        /// Validates the user.
        /// </summary>
        /// <returns><c>true</c>, if user was validated, <c>false</c> otherwise.</returns>
        /// <param name="user">The username attept</param>
        /// <param name="passwordAttempt">The password attempt</param>
        protected bool ValidateUser(string user, string passwordAttempt)
        {
            string hash;
            if (!_passwordMap.TryGetValue(user, out hash)) {
                return false;
            }

            return PasswordHash.ValidatePassword(passwordAttempt, hash);
        }

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


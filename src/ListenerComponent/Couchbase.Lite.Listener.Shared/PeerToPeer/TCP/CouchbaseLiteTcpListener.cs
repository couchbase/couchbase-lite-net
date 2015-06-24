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
using System.Net;
using System.Security.Principal;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace Couchbase.Lite.Listener.Tcp
{
    /// <summary>
    /// An implementation of CouchbaseLiteServiceListener using TCP/IP
    /// </summary>
    public sealed class CouchbaseLiteTcpListener : CouchbaseLiteServiceListener
    {

        #region Constants

        private const int NONCE_TIMEOUT = 300;

        #endregion

        #region Variables 

        private readonly HttpListener _listener;
        private readonly string _realm;
        private Manager _manager;
        private static HashSet<string> _RecentNonces = new HashSet<string>();
        private static Dictionary<string, Tuple<string, int>> _InUseNonces = new Dictionary<string, Tuple<string, int>>();

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="manager">The manager to use for opening DBs, etc</param>
        /// <param name="port">The port to listen on</param>
        /// <remarks>
        /// If running on Windows, check <a href="https://github.com/couchbase/couchbase-lite-net/wiki/Gotchas">
        /// This document</a>
        /// </remarks>
        public CouchbaseLiteTcpListener(Manager manager, ushort port, string realm = "Couchbase")
            : this(manager, port, false, realm)
        {
            
        }

        public CouchbaseLiteTcpListener(Manager manager, ushort port, bool useTLS, string realm = "Couchbase")
        {
            _manager = manager;
            _realm = realm;
            _listener = new HttpListener();
            string prefix = String.Format("http://*:{0}/", port);
            _listener.Prefixes.Add(prefix);
        }

        #endregion

        #region Private Methods

        private static string GenerateNonce()
        {
            var nonce = Misc.CreateGUID();

            // We have to remember that the HTTP protocol is stateless.
            // Even though with version 1.1 persistent connections are the norm, they are not guaranteed.
            // Thus if we generate a nonce for this connection,
            // it should be honored for other connections in the near future.
            // 
            // In fact, this is absolutely necessary in order to support QuickTime.
            // When QuickTime makes it's initial connection, it will be unauthorized, and will receive a nonce.
            // It then disconnects, and creates a new connection with the nonce, and proper authentication.
            // If we don't honor the nonce for the second connection, QuickTime will repeat the process and never connect.
            _RecentNonces.Add(nonce);
            Task.Delay(TimeSpan.FromSeconds(NONCE_TIMEOUT)).ContinueWith(t => _RecentNonces.Remove(nonce));

            return nonce;
        }

        //This gets called when the listener receives a request
        private void ProcessContext(HttpListenerContext context)
        {
            var client = context.Request.RemoteEndPoint.ToString();
            _listener.GetContextAsync().ContinueWith(t => ProcessContext(t.Result));
            if (RequiresAuth && !PerformAuthorization(context)) {
                RespondUnauthorized(context);
                return;
            }

            _router.HandleRequest(new CouchbaseListenerTcpContext(context, _manager));
        }

        private void RespondUnauthorized(HttpListenerContext context)
        {
            var response = context.Response;
            response.StatusCode = 401;
            response.ContentLength64 = 0;
            string challenge = String.Format("Digest realm=\"{0}\", qop=\"auth\", nonce=\"{1}\"", _realm, GenerateNonce());;
            response.AddHeader("WWW-Authenticate", challenge);
            response.Close();
        }

        private bool PerformAuthorization(HttpListenerContext context)
        {
            var authorizationHeader = context.Request.Headers["Authorization"];
            if (authorizationHeader == null) {
                return false;
            }

            if (authorizationHeader.StartsWith("Basic")) {
                return DoBasicAuth(authorizationHeader);
            } 

            return DoDigestAuth(context);
        }

        private bool DoBasicAuth(string authorizationHeader)
        {
            return ValidateUser(authorizationHeader);
        }

        private bool DoDigestAuth(HttpListenerContext context)
        {
            var client = context.Request.RemoteEndPoint.ToString().Split(':')[0];
            var parsedHeader = new DigestAuthHeaderValue(context);
            if (_InUseNonces.ContainsKey(client) && _InUseNonces[client].Item1 == parsedHeader.Nonce) {
                int lastNc = _InUseNonces[client].Item2;
                if (lastNc >= parsedHeader.Nc) {
                    // possible replay attack
                    return false;
                }
            } else if(!_RecentNonces.Contains(parsedHeader.Nonce)){
                return false; // Stale / non-issued nonce
            }

            _InUseNonces[client] = Tuple.Create(parsedHeader.Nonce, parsedHeader.Nc);
            _RecentNonces.Remove(parsedHeader.Nonce);
            return ValidateUser(parsedHeader);
        }

        #endregion

        #region Overrides

        public override void Start()
        {
            if (_listener.IsListening) {
                return;
            }

            _listener.Start();
            _listener.GetContextAsync().ContinueWith((t) => ProcessContext(t.Result));
        }

        public override void Stop()
        {
            if (!_listener.IsListening) {
                return;
            }

            _listener.Stop();
        }

        public override void Abort()
        {
            if (!_listener.IsListening) {
                return;
            }

            _listener.Abort();
        }

        protected override void DisposeInternal()
        {
            ((IDisposable)_listener).Dispose();
        }

        #endregion

    }
}


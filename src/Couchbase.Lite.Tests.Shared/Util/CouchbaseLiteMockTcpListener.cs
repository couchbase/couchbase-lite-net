//
//  CouchbaseLiteServiceListener.cs
//
//  Author:
//      Jim Borden  <jim.borden@couchbase.com>
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

using System.Threading.Tasks;
using Couchbase.Lite.Listener;
using WebSocketSharp.Net;
using Couchbase.Lite.Listener.Tcp;

namespace Couchbase.Lite.Tests
{
    public sealed class CouchbaseLiteMockTcpListener : CouchbaseLiteServiceListener
    {

        #region Constants

        private const string TAG = "CouchbaseLiteMockTcpListener";

        #endregion

        #region Variables 

        private readonly HttpListener _listener;

        #endregion

        #region Properties

        public Func<HttpListenerContext, ICouchbaseListenerContext> ContextGenerator { get; set; }

        #endregion

        #region Constructors

        public CouchbaseLiteMockTcpListener(ushort port)
        {
            _listener = new HttpListener();
            string prefix = String.Format("http://*:{0}/", port);
            _listener.Prefixes.Add(prefix);
            HttpListener.DefaultServerString = "Couchbase Lite " + Manager.VersionString;
        }

        #endregion

        #region Private Methods

        //This gets called when the listener receives a request
        private void ProcessRequest (HttpListenerContext context)
        {
            var getContext = Task.Factory.FromAsync<HttpListenerContext>(_listener.BeginGetContext, _listener.EndGetContext, null);
            getContext.ContinueWith(t => ProcessRequest(t.Result));

            _router.HandleRequest(ContextGenerator(context));
        }

        #endregion

        #region Overrides

        public override void Start()
        {
            if (_listener.IsListening) {
                return;
            }

            try {
                _listener.Start();
            } catch (HttpListenerException) {
                throw new InvalidOperationException("The process cannot bind to the port.  Please use netsh to authorize the route as an administrator.  For " +
                    "more details see https://github.com/couchbase/couchbase-lite-net/wiki/Gotchas");
            }

            var getContext = Task.Factory.FromAsync<HttpListenerContext>(_listener.BeginGetContext, _listener.EndGetContext, null);
            getContext.ContinueWith(t => ProcessRequest(t.Result));
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

            _listener.Stop();
        }

        protected override void DisposeInternal()
        {
            ((IDisposable)_listener).Dispose();
        }

        #endregion

    }
}


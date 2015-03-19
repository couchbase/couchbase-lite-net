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
using Couchbase.Lite.Util;

namespace Couchbase.Lite.PeerToPeer
{
    public class CouchbaseLiteServiceListener : IDisposable
    {
        private readonly HttpListener _listener;
        private bool _disposed;

        public CouchbaseLiteServiceListener()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://*:4984/");
        }

        public void Start()
        {
            if (_listener.IsListening) {
                return;
            }
                
            _listener.Start();
            _listener.GetContextAsync().ContinueWith((t) => ProcessContext(t.Result));
        }

        public void Stop()
        {
            if (!_listener.IsListening) {
                return;
            }
                
            _listener.Stop();
        }

        public void Abort()
        {
            if (!_listener.IsListening) {
                return;
            }
                
            _listener.Abort();
        }

        private void ProcessContext(HttpListenerContext context)
        {
            _listener.GetContextAsync().ContinueWith((t) => ProcessContext(t.Result));
            CouchbaseLiteRouter.HandleContext(context);
        }

        public void Dispose()
        {
            if (_disposed) {
                return;
            }

            _disposed = true;
            ((IDisposable)_listener).Dispose();
        }
    }
}


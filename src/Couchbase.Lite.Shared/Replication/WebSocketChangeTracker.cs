//
// WebSocketChangeTracker.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Util;
using WebSocketSharp;
using Couchbase.Lite.Auth;
using System.Collections.Generic;
using Microsoft.IO;
using System.Security.Authentication;
using System.Net;

namespace Couchbase.Lite.Internal
{
    internal enum ChangeTrackerMessageType : byte
    {
        Unknown,
        Plaintext,
        GZip,
        EOF
    }

    // Concrete class for receiving changes over web sockets
    internal class WebSocketChangeTracker : ChangeTracker
    {
        
        #region Constants

        private static readonly string Tag = typeof(WebSocketChangeTracker).Name;

        #endregion

        #region Variables

        private WebSocket _client;
        private CancellationTokenSource _cts;

        #endregion

        #region Properties

        public bool CanConnect { get; set; }

        public override Uri ChangesFeedUrl
        {
            get {
                var dbURLString = DatabaseUrl.ToString().Replace("http", "ws");
                if (!dbURLString.EndsWith("/", StringComparison.Ordinal)) {
                    dbURLString += "/";
                }

                dbURLString += "_changes?feed=websocket";
                return new Uri(dbURLString);
            }
        }

        #endregion

        #region Constructors

        public WebSocketChangeTracker(ChangeTrackerOptions options) : base(options)
        {
            _responseLogic = new WebSocketLogic();
            CanConnect = true;
        }

        #endregion

        #region Private Methods

        // Possibly unused, never seen it called
        private void OnError(object sender, WebSocketSharp.ErrorEventArgs args)
        {
            Log.To.ChangeTracker.I(Tag, String.Format("{0} remote error {1}", this, args.Message), args.Exception);
        }

        // Called when the web socket connection is closed
        private void OnClose(object sender, CloseEventArgs args)
        {
            if (_client != null) {
                if (args.Code == (ushort)CloseStatusCode.ProtocolError) {
                    // This is not a valid web socket connection, need to fall back to regular HTTP
                    CanConnect = false;
                    Stopped(ErrorResolution.RetryNow);
                } else {
                    if(Backoff.CanContinue) {
                        Log.To.ChangeTracker.I(Tag, "{0} remote {1} closed connection ({2} {3})",
                            this, args.WasClean ? "cleanly" : "forcibly", args.Code, args.Reason);
                        Backoff.DelayAppropriateAmountOfTime().ContinueWith(t => _client?.ConnectAsync());
                    } else {
                        Stopped(ErrorResolution.RetryLater);
                    }
                }
            } else {
                Log.To.ChangeTracker.I(Tag, "{0} is closed", this);
                Stopped(ErrorResolution.Stop);
            }
        }

        // Called when the web socket establishes a connection
        private void OnConnect(object sender, EventArgs args)
        {
            if (_cts.IsCancellationRequested) {
                Log.To.ChangeTracker.I(Tag, "{0} Cancellation requested, aborting in OnConnect", this);
                return;
            }

            Misc.SafeDispose(ref _responseLogic);
            _responseLogic = new WebSocketLogic();
            _responseLogic.OnCaughtUp = () => Client?.ChangeTrackerCaughtUp(this);
            _responseLogic.OnChangeFound = (change) =>
            {
                if (!ReceivedChange(change)) {
                    Log.To.ChangeTracker.W(Tag,  String.Format("change is not parseable"));
                }
            };

            Backoff.ResetBackoff();
            Log.To.ChangeTracker.V(Tag, "{0} websocket opened", this);

            // Now that the WebSocket is open, send the changes-feed options (the ones that would have
            // gone in the POST body if this were HTTP-based.)
            var bytes = GetChangesFeedPostBody().ToArray();
            _client?.SendAsync(bytes, null);
        }

        // Called when a message is received
        private void OnReceive(object sender, MessageEventArgs args)
        {
            if (_cts.IsCancellationRequested) {
                Log.To.ChangeTracker.I(Tag, "{0} Cancellation requested, aborting in OnReceive", this);
                return;
            }

            if (args.IsPing) {
                _client.Ping();
                return;
            }
                
            try {
                if(args.RawData.Length == 0) {
                    return;
                }

                var code = ChangeTrackerMessageType.Unknown;
                if(args.IsText) {
                    if(args.RawData.Length == 2 && args.RawData[0] == '[' && args.RawData[1] == ']') {
                        code = ChangeTrackerMessageType.EOF;
                    } else {
                        code = ChangeTrackerMessageType.Plaintext;
                    }
                } else {
                    code = ChangeTrackerMessageType.GZip;
                }

                var responseStream = RecyclableMemoryStreamManager.SharedInstance.GetStream("WebSocketChangeTracker", args.RawData.Length + 1);
                try {
                    responseStream.WriteByte((byte)code);
                    responseStream.Write(args.RawData, 0, args.RawData.Length);
                    responseStream.Seek(0, SeekOrigin.Begin);
                    _responseLogic.ProcessResponseStream(responseStream, _cts.Token);
                } finally {
                    responseStream.Dispose();
                }
            } catch(Exception e) {
                Log.To.ChangeTracker.E(Tag, String.Format("{0} is not parseable", GetLogString(args)), e);
            }
        }

        private string GetLogString(MessageEventArgs args)
        {
            if(args.IsBinary) {
                return "<gzip stream>";
            } else if(args.IsText) {
                return args.Data;
            }

            return null;
        }

        #endregion

        #region Overrides
            
        public override bool Start()
        {
            if (IsRunning) {
                return false;
            }

            IsRunning = true;
            Log.To.ChangeTracker.I(Tag, "Starting {0}...", this);
            _cts = new CancellationTokenSource();

            var authHeader = (_remoteSession.Authenticator as ICustomHeadersAuthorizer)?.AuthorizationHeaderValue;

            // A WebSocket has to be opened with a GET request, not a POST (as defined in the RFC.)
            // Instead of putting the options in the POST body as with HTTP, we will send them in an
            // initial WebSocket message
            _usePost = false;
            _caughtUp = false;
            _client = new WebSocket(ChangesFeedUrl.AbsoluteUri);
            var systemProxy = WebRequest.DefaultWebProxy.GetProxy(DatabaseUrl)?.ToString();
            var proxyCredentials = WebRequest.DefaultWebProxy.Credentials?.GetCredential(DatabaseUrl, "");
            if(systemProxy != null && proxyCredentials != null) {
                _client.SetProxy(systemProxy, proxyCredentials.UserName, proxyCredentials.Password);
            }

            _client.WaitTime = TimeSpan.FromSeconds(2);
            _client.OnOpen += OnConnect;
            _client.OnMessage += OnReceive;
            _client.OnError += OnError;
            _client.OnClose += OnClose;
            _client.SslConfiguration.EnabledSslProtocols = (SslProtocols)ServicePointManager.SecurityProtocol;
            _client.SslConfiguration.EnabledSslProtocols &= ~SslProtocols.Ssl3;
            _client.SslConfiguration.EnabledSslProtocols |= SslProtocols.Tls;    
            foreach(Cookie cookie in Client.GetCookieStore().GetCookies(ChangesFeedUrl)) {
                _client.SetCookie(new WebSocketSharp.Net.Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
            }

            var additionalHeaders = new Dictionary<string, string>();
            if (authHeader != null) {
                additionalHeaders["Authorization"] = authHeader.ToString();
            }

            foreach(var header in _remoteSession.RequestHeaders) {
                additionalHeaders[header.Key] = header.Value;
            }

            if(additionalHeaders.Any()) {
                _client.CustomHeaders = additionalHeaders;
            }

            _client.ConnectAsync();
            return true;
        }

        public override void Stop(object resolutionWrapper)
        {
            if (!IsRunning) {
                return;
            }

            IsRunning = false;
            Misc.SafeNull(ref _client, c =>
            {
                Log.To.ChangeTracker.I(Tag, "{0} requested to stop", this);
                c.CloseAsync(CloseStatusCode.Normal);
            });
        }

        protected override void Stopped(ErrorResolution resolution)
        {
            Client?.ChangeTrackerStopped(this, resolution);
            Misc.SafeDispose(ref _responseLogic);
        }

        #endregion
    }
}


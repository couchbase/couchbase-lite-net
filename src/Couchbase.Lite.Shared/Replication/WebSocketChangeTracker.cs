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
using System.Threading.Tasks;
using Couchbase.Lite.Util;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;
using System.IO;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.GZip;

namespace Couchbase.Lite.Internal
{
    internal class WebSocketChangeTracker : ChangeTracker
    {
        private static readonly string Tag = typeof(WebSocketChangeTracker).Name;
        private WebSocket _client;
        private CancellationTokenSource _cts;
        private ChunkedGZipChanges _changesProcessor;

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

        public WebSocketChangeTracker(Uri databaseURL, bool includeConflicts, object lastSequenceID, 
           IChangeTrackerClient client, TaskFactory workExecutor = null)
            : base(databaseURL, ChangeTrackerMode.WebSocket, includeConflicts, lastSequenceID, client, workExecutor)
        {
            
        }

        private void OnError(object sender, WebSocketSharp.ErrorEventArgs args)
        {
            Log.To.ChangeTracker.I(Tag, String.Format("{0} remote error {1}", this, args.Message), args.Exception);
        }

        private void OnClose(object sender, CloseEventArgs args)
        {
            if (_client != null) {
                Log.To.ChangeTracker.I(Tag, "{0} remote {1} closed connection ({2} {3})",
                    this, args.WasClean ? "cleanly" : "forcibly", args.Code, args.Reason);
                backoff.SleepAppropriateAmountOfTime();
                _client.ConnectAsync();
            } else {
                Log.To.ChangeTracker.I(Tag, "{0} is closed", this);
                Stopped();
            }
        }

        private void OnConnect(object sender, EventArgs args)
        {
            if (_cts.IsCancellationRequested) {
                Log.To.ChangeTracker.I(Tag, "{0} Cancellation requested, aborting in OnConnect", this);
                return;
            }

            backoff.ResetBackoff();
            Log.To.ChangeTracker.V(Tag, "{0} websocket opened", this);

            // Now that the WebSocket is open, send the changes-feed options (the ones that would have
            // gone in the POST body if this were HTTP-based.)
            var bytes = GetChangesFeedPostBody().ToArray();
            _client.SendAsync(bytes, null);
        }

        private void OnReceive(object sender, MessageEventArgs args)
        {
            if (_cts.IsCancellationRequested) {
                Log.To.ChangeTracker.I(Tag, "{0} Cancellation requested, aborting in OnReceive", this);
                return;
            }

            if (args.IsPing) {
                return;
            }
                
            try {
                if(args.IsBinary) {
                    if(_changesProcessor == null) {
                        _changesProcessor = new ChunkedGZipChanges(ChunkStyle.ByArray);
                        _changesProcessor.ChunkFound += (s, change) => 
                        {
                            if (!ReceivedChange(change)) {
                                Log.To.ChangeTracker.W(Tag,  String.Format("{0} is not parseable", GetLogString(args)));
                            }
                        };

                        _changesProcessor.Finished += (s, e) => 
                        {
                            if(!_caughtUp) {
                                _caughtUp = true;
                                Log.To.ChangeTracker.I(Tag, "{0} caught up to the end of the changes feed", this);
                                var client = Client;
                                if(client != null) {
                                    client.ChangeTrackerCaughtUp(this);
                                }
                            }

                            _changesProcessor.Dispose();
                        };
                    }

                    _changesProcessor.AddData(args.RawData);
                } else if(args.IsText) {
                    if(args.RawData.Length == 2) {
                        _changesProcessor.Complete();
                        return;
                    }

                    var change = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(args.RawData.Skip(1).Take(args.RawData.Length - 2));
                    if (!ReceivedChange(change)) {
                        Log.To.ChangeTracker.W(Tag,  String.Format("{0} is not parseable", GetLogString(args)));
                    }
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
            
        public override bool Start()
        {
            if (_client != null) {
                return false;
            }

            Log.To.ChangeTracker.I(Tag, "Starting {0}...", this);
            _cts = new CancellationTokenSource();

            // A WebSocket has to be opened with a GET request, not a POST (as defined in the RFC.)
            // Instead of putting the options in the POST body as with HTTP, we will send them in an
            // initial WebSocket message
            _usePost = false;
            _caughtUp = false;
            _client = new WebSocket(ChangesFeedUrl.AbsoluteUri);
            _client.OnOpen += OnConnect;
            _client.OnMessage += OnReceive;
            _client.OnError += OnError;
            _client.OnClose += OnClose;
            _client.ConnectAsync();

            return true;
        }

        public override void Stop()
        {
            var client = _client;
            _client = null;
            if (client != null) {
                Log.To.ChangeTracker.I(Tag, "{0} requested to stop", this);
                client.CloseAsync(CloseStatusCode.Normal);
            }
        }

        protected override void Stopped()
        {
            var client = Client;
            if (client != null) {
                client.ChangeTrackerStopped(this);
            }
        }
    }
}


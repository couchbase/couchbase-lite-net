// 
// WebSocketTransport.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.Threading;
using Couchbase.Lite.Logging;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite.Sync
{
    internal static unsafe class WebSocketTransport
    {
        #region Constants

        private const string Tag = nameof(WebSocketTransport);

        [NotNull]
        private static readonly Dictionary<int, WebSocketWrapper> Sockets = new Dictionary<int, WebSocketWrapper>();

        #endregion

        #region Variables

        private static int _NextID;

        #endregion

        #region Public Methods

        public static void RegisterWithC4()
        {
            SocketFactory.RegisterFactory(DoOpen, DoClose, DoWrite, DoCompleteReceive);
            SocketFactory.SetErrorHandler(DoError);
        }

        #endregion

        #region Private Methods

        private static void DoClose(C4Socket* socket)
        {
            var id = (int) socket->nativeHandle;
            if (Sockets.TryGetValue(id, out var socketWrapper)) {
                socketWrapper.CloseSocket();
                Sockets.Remove(id);
            } else {
                Log.To.Sync.W(Tag, "Invalid call to DoClose; socket does not exist (or was closed)");
            }
        }

        private static void DoCompleteReceive(C4Socket* socket, ulong bytecount)
        {
            var id = (int)socket->nativeHandle;

            if (Sockets.TryGetValue(id, out var socketWrapper)) {
                socketWrapper.CompletedReceive(bytecount);
            } else {
                Log.To.Sync.W(Tag, "Invalid call to DoCompleteReceive; socket does not exist (or was closed)");
            }
        }

        private static void DoError(C4Socket* socket, Exception e)
        {
            Log.To.Sync.E(Tag, "Websocket Error", e);
        }

        private static void DoOpen(C4Socket* socket, C4Address* address, C4Slice options)
        {
            var builder = new UriBuilder {
                Host = address->hostname.CreateString(),
                Scheme = address->scheme.CreateString(),
                Port = address->port,
                Path = address->path.CreateString()
            };

            Uri uri;
            try {
                uri = builder.Uri;
            } catch (Exception) {
                Native.c4socket_closed(socket, new C4Error(C4ErrorCode.InvalidParameter));
                return;
            }

            if (uri == null) {
                Native.c4socket_closed(socket, new C4Error(C4ErrorCode.InvalidParameter));
                return;
            }

            var opts =
                FLSliceExtensions.ToObject(NativeRaw.FLValue_FromTrustedData((FLSlice) options)) as
                    Dictionary<string, object>;
            var replicationOptions = new ReplicatorOptionsDictionary(opts);
            var socketWrapper = new WebSocketWrapper(uri, socket, replicationOptions);
            var id = Interlocked.Increment(ref _NextID);
            socket->nativeHandle = (void*)id;
            Sockets[id] = socketWrapper;
            socketWrapper.Start();
        }

        private static void DoWrite(C4Socket* socket, byte[] data)
        {
            var id = (int)socket->nativeHandle;
            if (Sockets.TryGetValue(id, out var socketWrapper)) {
                socketWrapper.Write(data);
            } else {
                Log.To.Sync.W(Tag, "Invalid call to DoWrite; socket does not exist (or was closed)");
            }
        }

        #endregion
    }
}

﻿// 
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

using Couchbase.Lite.Internal.Logging;

using LiteCore.Interop;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Couchbase.Lite.Sync
{
    internal static unsafe class WebSocketTransport
    {
        #region Constants

        private const string Tag = nameof(WebSocketTransport);

        private static readonly ConcurrentDictionary<int, WebSocketWrapper> Sockets = new ConcurrentDictionary<int, WebSocketWrapper>();

        #endregion

        #region Variables

        private static int _NextID;

        #endregion

        #region Public Methods

        public static void RegisterWithC4()
        {
            SocketFactory.RegisterFactory(DoOpen, DoClose, DoWrite, DoCompleteReceive, DoDispose);
            SocketFactory.SetErrorHandler(DoError);
        }

        #endregion

        #region Private Methods

        private static void DoClose(C4Socket* socket)
        {
            var id = (int) Native.c4Socket_getNativeHandle(socket);
            if (Sockets.TryGetValue(id, out var socketWrapper)) {
                socketWrapper.CloseSocket();
            } else {
                WriteLog.To.Sync.W(Tag, "Invalid call to DoClose; socket does not exist (or was disposed)");
            }
        }

        private static void DoCompleteReceive(C4Socket* socket, ulong bytecount)
        {
            var id = (int)Native.c4Socket_getNativeHandle(socket);

            if (Sockets.TryGetValue(id, out var socketWrapper)) {
                socketWrapper.CompletedReceive(bytecount);
            } else {
                WriteLog.To.Sync.W(Tag, "Invalid call to DoCompleteReceive; socket does not exist (or was closed)");
            }
        }

        private static void DoDispose(C4Socket* socket)
        {
            var id = (int)Native.c4Socket_getNativeHandle(socket);
            Sockets.TryRemove(id, out var tmp);
        }

        private static void DoError(C4Socket* socket, Exception e)
        {
            WriteLog.To.Sync.E(Tag, "Websocket Error", e);
        }

        private static void DoOpen(C4Socket* socket, C4Address* address, FLSlice options, void* context)
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
                FLSliceExtensions.ToObject(NativeRaw.FLValue_FromData((FLSlice) options, FLTrust.Trusted)) as
                    Dictionary<string, object?>;
            Debug.Assert(opts != null);
            var replicationOptions = new ReplicatorOptionsDictionary(opts!);
            
            var id = Interlocked.Increment(ref _NextID);
            Native.c4Socket_setNativeHandle(socket, (void*)id);
            var socketWrapper = new WebSocketWrapper(uri, socket, replicationOptions);
            var replicator = GCHandle.FromIntPtr((IntPtr) context).Target as Replicator;
            replicator?.WatchForCertificate(socketWrapper);
            replicator?.CheckForCookiesToSet(socketWrapper);
            Sockets.AddOrUpdate(id, socketWrapper, (k, v) => socketWrapper);
            socketWrapper.Start();
        }

        private static void DoWrite(C4Socket* socket, byte[] data)
        {
            var id = (int)Native.c4Socket_getNativeHandle(socket);
            if (Sockets.TryGetValue(id, out var socketWrapper)) {
                socketWrapper.Write(data);
            } else {
                WriteLog.To.Sync.W(Tag, "Invalid call to DoWrite; socket does not exist (or was closed)");
            }
        }

        #endregion
    }
}

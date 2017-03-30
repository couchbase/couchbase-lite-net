using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Couchbase.Lite.Logging;
using LiteCore.Interop;

namespace Couchbase.Lite.Sync
{
    internal static unsafe class WebSocketTransport
    {
        private static readonly Dictionary<int, WebSocketWrapper> _Sockets = new Dictionary<int, WebSocketWrapper>();
        private static int _NextID = 0;

        public static void RegisterWithC4()
        {
            SocketFactory.RegisterFactory(DoOpen, DoClose, DoWrite, DoCompleteReceive);
        }

        private static void DoCompleteReceive(C4Socket* socket, ulong bytecount)
        {
            var id = (int)socket->nativeHandle;
            var socketWrapper = _Sockets[id];
            socketWrapper.CompletedReceive(bytecount);
        }

        private static void DoOpen(C4Socket* socket, C4Address* address)
        {
            var builder = new UriBuilder {
                Host = address->hostname.CreateString(),
                Scheme = address->scheme.CreateString(),
                Port = address->port,
                Path = address->path.CreateString()
            };

            Uri uri = null;
            try {
                uri = builder.Uri;
            } catch (Exception) {
                Native.c4socket_closed(socket, new C4Error(LiteCoreError.InvalidParameter));
                return;
            }

            if (uri == null) {
                Native.c4socket_closed(socket, new C4Error(LiteCoreError.InvalidParameter));
                return;
            }

            var socketWrapper = new WebSocketWrapper(uri, socket);
            var id = Interlocked.Increment(ref _NextID);
            socket->nativeHandle = (void*)id;
            _Sockets[id] = socketWrapper;
            socketWrapper.Start();
        }

        private static void DoClose(C4Socket* socket)
        {
            var id = (int) socket->nativeHandle;
            var socketWrapper = _Sockets[id];
            socketWrapper.CloseSocket();
        }

        private static void DoWrite(C4Socket* socket, byte[] data)
        {
            var id = (int)socket->nativeHandle;
            var socketWrapper = _Sockets[id];
            socketWrapper.Write(data);
        }
    }
}

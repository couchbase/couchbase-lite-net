using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;

using Couchbase.Lite.Support;
using LiteCore.Interop;

namespace Couchbase.Lite.Sync
{
    internal sealed unsafe class WebSocketWrapper
    {
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(300);
        private const uint MaxReceivedBytesPending = 100 * 1024;

        private readonly C4Socket* _socket;
        private readonly Uri _url;
        private readonly SerialQueue _queue = new SerialQueue();
        private readonly SerialQueue _c4Queue = new SerialQueue();
        private readonly byte[] _buffer = new byte[MaxReceivedBytesPending];
        private bool _receiving;
        private uint _receivedBytesPending;

        public ClientWebSocket WebSocket { get; } = new ClientWebSocket();

        public WebSocketWrapper(Uri url, C4Socket* socket)
        {
            _socket = socket;
            _url = url;
        }

        public void Start()
        {
            _queue.DispatchAsync(() =>
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(ConnectTimeout);
                WebSocket.ConnectAsync(_url, cts.Token).ContinueWith(t =>
                {
                    Receive();
                    _c4Queue.DispatchAsync(() =>
                    {
                        Native.c4socket_opened(_socket);
                    });
                }, cts.Token);
            });
        }

        public void CloseSocket()
        {
            _queue.DispatchAsync(() =>
            {
                WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client requested close",
                    CancellationToken.None).ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                {
                    _c4Queue.DispatchAsync(() =>
                    {
                        Native.c4socket_closed(_socket, new C4Error());
                    });
                });
            });
        }

        public void Write(byte[] data)
        {
            _queue.DispatchAsync(() =>
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(IdleTimeout);
                WebSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, cts.Token)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .OnCompleted(
                        () =>
                        {
                            _c4Queue.DispatchAsync(() =>
                            {
                                Native.c4socket_completedWrite(_socket, (ulong) data.Length);
                            });
                        });
            });
        }

        public void CompletedReceive(ulong byteCount)
        {
            _queue.DispatchAsync(() =>
            {
                _receivedBytesPending -= (uint)byteCount;
                Receive();
            });
        }

        private void Receive()
        {
            if (_receiving) {
                return;
            }

            _receiving = true;
            var cts = new CancellationTokenSource();
            cts.CancelAfter(IdleTimeout);
            WebSocket.ReceiveAsync(new ArraySegment<byte>(_buffer), cts.Token)
                .ContinueWith(t =>
                {
                    if (t.IsCanceled) {
                        return;
                    }

                    if (t.Exception != null) {
                        // TODO
                        return;
                    }

                    _receivedBytesPending += (uint)t.Result.Count;
                    var data = _buffer.Take(t.Result.Count).ToArray();
                    _c4Queue.DispatchAsync(() =>
                    {
                        Native.c4socket_received(_socket, data);
                    });
                    if (!t.Result.EndOfMessage && _receivedBytesPending < MaxReceivedBytesPending) {
                        Receive();
                    }
                }, cts.Token);

        }
    }
}

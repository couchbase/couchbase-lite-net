// 
// WebSocketWrapper.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
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
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using LiteCore.Interop;

namespace Couchbase.Lite.Sync
{
    internal sealed unsafe class WebSocketWrapper
    {
        #region Constants

        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(300);
        private const uint MaxReceivedBytesPending = 100 * 1024;
        private const string Tag = nameof(WebSocketWrapper);

        #endregion

        #region Variables

        private readonly byte[] _buffer = new byte[MaxReceivedBytesPending];
        private readonly SerialQueue _c4Queue = new SerialQueue();
        private readonly List<byte> _currentMessage = new List<byte>();
        private readonly SerialQueue _queue = new SerialQueue();

        private readonly C4Socket* _socket;
        private readonly Uri _url;
        private uint _receivedBytesPending;
        private bool _receiving;

        #endregion

        #region Properties

        public ClientWebSocket WebSocket { get; } = new ClientWebSocket();

        #endregion

        #region Constructors

        public WebSocketWrapper(Uri url, C4Socket* socket)
        {
            WebSocket.Options.AddSubProtocol("BLIP");
            _socket = socket;
            _url = url;
        }

        #endregion

        #region Public Methods

        public void CloseSocket(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string message = null)
        {
            _queue.DispatchAsync(() =>
            {
                WebSocket.CloseAsync(status, message,
                    CancellationToken.None).ContinueWith(t => 
                {
                    if (t.IsCanceled || t.Exception != null) {
                        return;
                    }

                    _c4Queue.DispatchAsync(() =>
                    {
                        Native.c4socket_closed(_socket, new C4Error());
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

        public void Start()
        {
            _queue.DispatchAsync(() =>
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(ConnectTimeout);
                WebSocket.ConnectAsync(_url, cts.Token).ContinueWith(t =>
                {
                    if (t.IsCanceled) {
                        Native.c4socket_closed(_socket, new C4Error());
                        return;
                    }

                    if (t.Exception != null) {
                        Native.c4socket_closed(_socket, new C4Error(LiteCoreError.UnexpectedError));
                        return;
                    }

                    _c4Queue.DispatchAsync(() =>
                    {
                        Native.c4socket_opened(_socket);
                        Receive();
                    });
                }, cts.Token);
            });
        }

        public void Write(byte[] data)
        {
            _queue.DispatchAsync(() =>
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(IdleTimeout);
                if (data[0] == 8) {
                    var statusBytes = new byte[2];
                    statusBytes[0] = data[1];
                    statusBytes[1] = data[2];
                    string message = null;
                    if (data.Length > 3) {
                        message = Encoding.UTF8.GetString(data, 3, data.Length - 3);
                    }
                    CloseSocket((WebSocketCloseStatus)BitConverter.ToUInt16(statusBytes, 0), message);
                    return;
                }

                if (data[0] != 2) {
                    Log.To.Sync.W(Tag, $"Bogus message type from LiteCore ({data[0]}).  Should be 2 or 8, ignoring...");
                    return;
                }

                WebSocket.SendAsync(new ArraySegment<byte>(data, 1, data.Length - 1), WebSocketMessageType.Binary, true, cts.Token)
                    .ContinueWith(
                        t =>
                        {
                            if (t.IsCanceled || t.Exception != null) {
                                return;
                            }

                            _c4Queue.DispatchAsync(() =>
                            {
                                Native.c4socket_completedWrite(_socket, (ulong) data.Length);
                            });
                        }, cts.Token);
            });
        }

        #endregion

        #region Private Methods

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
                    _receiving = false;
                    if (t.IsCanceled) {
                        return;
                    }

                    if (t.Exception != null) {
                        // TODO
                        return;
                    }
                    
                    if (!t.Result.EndOfMessage && _receivedBytesPending < MaxReceivedBytesPending) {
                        Receive();
                    } else if (t.Result.EndOfMessage) {
                        byte[] data;
                        if (_currentMessage.Any()) {
                            _currentMessage.AddRange(_buffer.Take(t.Result.Count));
                            data = _currentMessage.ToArray();
                            _currentMessage.Clear();
                        } else {
                            data = _buffer.Take(t.Result.Count).ToArray();
                        }

                        _receivedBytesPending += (uint)t.Result.Count;
                        _c4Queue.DispatchAsync(() =>
                        {
                            Native.c4socket_received(_socket, data);
                        });
                    }
                }, cts.Token);

        }

        #endregion
    }
}

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
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
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
		private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);
		private readonly ManualResetEventSlim _connected = new ManualResetEventSlim();
        private readonly IReadOnlyDictionary<string, object> _options;

        private readonly C4Socket* _socket;
        private readonly Uri _url;
        private uint _receivedBytesPending;
        private bool _receiving;

        #endregion

        #region Properties

        public ClientWebSocket WebSocket { get; } = new ClientWebSocket();

        #endregion

        #region Constructors

        public WebSocketWrapper(Uri url, C4Socket* socket, IReadOnlyDictionary<string, object> options)
        {
            WebSocket.Options.AddSubProtocol("BLIP");
            _socket = socket;
            _url = url;
            _options = options;
        }

        #endregion

        #region Public Methods

        public void CloseSocket(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string message = null)
		{
            _queue.DispatchAsync(() =>
            {
				_connected.Wait();
				var waitForReply = WebSocket.State == WebSocketState.Open;
				if (!waitForReply)
				{
					WebSocket.CloseAsync(status, message,
						CancellationToken.None).ContinueWith(t =>
					{
						if (t.IsCanceled || t.Exception != null) {
							return;
						}

						_c4Queue.DispatchAsync(() =>
						{
							Native.c4socket_closed(_socket, new C4Error(C4ErrorDomain.WebSocketDomain, (int)WebSocket.CloseStatus));
						});
					});
				} else {
					WebSocket.CloseOutputAsync(status, message, CancellationToken.None);
				}
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
                SetupAuth();
                var cts = new CancellationTokenSource();
                cts.CancelAfter(ConnectTimeout);
                WebSocket.ConnectAsync(_url, cts.Token).ContinueWith(t =>
                {
                    if (t.IsCanceled) {
						// TODO: Cancel status?
                        Native.c4socket_closed(_socket, new C4Error(LiteCoreError.UnexpectedError));
                        return;
                    }

                    if (t.Exception != null) {
                        Native.c4socket_closed(_socket, new C4Error(LiteCoreError.UnexpectedError));
                        return;
                    }

					_connected.Set();
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
				_connected.Wait();
                var cts = new CancellationTokenSource();
                cts.CancelAfter(IdleTimeout);
				_mutex.Wait(cts.Token);
                WebSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, cts.Token)
                    .ContinueWith(t =>
					{
						_mutex.Release();
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

					if(t.Result.MessageType == WebSocketMessageType.Close) {
						if(WebSocket.State == WebSocketState.CloseSent) {
							Native.c4socket_closed(_socket, new C4Error(C4ErrorDomain.WebSocketDomain, (int)t.Result.CloseStatus));
						} else {
							CloseSocket(t.Result.CloseStatus == null ? WebSocketCloseStatus.Empty : t.Result.CloseStatus.Value, t.Result.CloseStatusDescription);
						}

						return;
					}
	

					if (!t.Result.EndOfMessage && _receivedBytesPending < MaxReceivedBytesPending)
						{
							_currentMessage.AddRange(_buffer.Take(t.Result.Count));
							Receive();
						}
						else if (t.Result.EndOfMessage)
						{
							byte[] data;
							if (_currentMessage.Any())
							{
								_currentMessage.AddRange(_buffer.Take(t.Result.Count));
								data = _currentMessage.ToArray();
								_currentMessage.Clear();
							}
							else
							{
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

        private void SetupAuth()
        {
            var auth = _options?.Get(ReplicationOptionKeys.AuthOption) as IDictionary<string, object>;
            if (auth != null) {
                var username = auth.GetCast<string>(ReplicationOptionKeys.AuthUsername);
                var password = auth.GetCast<string>(ReplicationOptionKeys.AuthPassword);
                if (username != null && password != null) {
                    WebSocket.Options.Credentials = new NetworkCredential(username, password);
                }
            }
        }

        #endregion
    }
}

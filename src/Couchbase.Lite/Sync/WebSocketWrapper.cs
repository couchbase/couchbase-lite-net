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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using LiteCore.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Lite.Sync
{
    internal sealed class WebSocketWrapper
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
        private readonly AutoResetEvent _readMutex = new AutoResetEvent(true);
        private readonly AutoResetEvent _writeMutex = new AutoResetEvent(true);
		private readonly ManualResetEventSlim _connected = new ManualResetEventSlim();
        private readonly HTTPLogic _logic;
        private readonly ReplicatorOptionsDictionary _options;
        private TcpClient _client;

        private readonly unsafe C4Socket* _socket;
        private string _expectedAcceptHeader;
        private uint _receivedBytesPending;
        private bool _receiving;

        #endregion

        #region Properties

        public Stream NetworkStream { get; private set; }

        #endregion

        #region Constructors

        public unsafe WebSocketWrapper(Uri url, C4Socket* socket, ReplicatorOptionsDictionary options)
        {
            _socket = socket;
            _logic = new HTTPLogic(url);
            _options = options;

            SetupAuth();
        }

        #endregion

        #region Public Methods

        public unsafe void CloseSocket()
		{
            _queue.DispatchAsync(() =>
            {
				_connected.Wait();
                ResetConnections();
                _c4Queue.DispatchAsync(() => Native.c4socket_closed(_socket, new C4Error()));
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

        public unsafe void Start()
        {
            _queue.DispatchAsync(() =>
            {
                if (_client != null) {
                    return;
                }

                // ReSharper disable once UseObjectOrCollectionInitializer
                _client = new TcpClient(AddressFamily.InterNetworkV6)
                {
                    SendTimeout = (int)IdleTimeout.TotalMilliseconds,
                    ReceiveTimeout = (int)IdleTimeout.TotalMilliseconds
                };

                _client.Client.DualMode = true;
                var cts = new CancellationTokenSource();
                cts.CancelAfter(ConnectTimeout);
                _client.ConnectAsync(_logic.UrlRequest.Host, _logic.UrlRequest.Port).ContinueWith(t =>
                {
                    if (!NetworkTaskSuccessful(t)) {
                        if (t.IsCanceled) {
                            Native.c4socket_closed(_socket, new C4Error(C4NetworkErrorCode.Timeout));
                        } else {
                            C4Error err;
                            Status.ConvertError(t.Exception.Flatten().InnerException, &err);
                            Native.c4socket_closed(_socket, err);
                        }
                        return;
                    }
                    _queue.DispatchAsync(StartInternal);
                }, cts.Token);
            });
        }

        public unsafe void Write(byte[] data)
        {
            _queue.DispatchAsync(() =>
            {
				_connected.Wait();
                _writeMutex.WaitOne();
                var cts = new CancellationTokenSource();
                cts.CancelAfter(IdleTimeout);
                NetworkStream.WriteAsync(data, 0, data.Length, cts.Token)
                    .ContinueWith(t =>
                    {
                        _writeMutex.Set();
                        if (!NetworkTaskSuccessful(t)) {
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

        private static string Base64Digest(string input)
        {
            var data = Encoding.ASCII.GetBytes(input);
            var engine = SHA1.Create();
            var hashed = engine.ComputeHash(data);
            return Convert.ToBase64String(hashed);
        }

        private void StartInternal()
        {
            Log.To.Sync.I(Tag, $"WebSocket connecting to {_logic.UrlRequest.Host}:{_logic.UrlRequest.Port}");
            var rng = RandomNumberGenerator.Create();
            var nonceBytes = new byte[16];
            rng.GetBytes(nonceBytes);
            var nonceKey = Convert.ToBase64String(nonceBytes);
            _expectedAcceptHeader = Base64Digest(nonceKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");

            foreach (var header in _options.Headers) {
                _logic[header.Key] = header.Value as string;
            }

            _logic["Cookie"] = _options.CookieString;
            _logic["Connection"] = "Upgrade";
            _logic["Upgrade"] = "websocket";
            _logic["Sec-WebSocket-Version"] = "13";
            _logic["Sec-WebSocket-Key"] = nonceKey;

            if (_logic.UseTls) {
                var stream = Service.Provider.GetRequiredService<ISslStreamFactory>().Create(_client.GetStream());
                stream.PinnedServerCertificate = _options.PinnedServerCertificate;
                stream.ConnectAsync(_logic.UrlRequest.Host, (ushort)_logic.UrlRequest.Port, null, false).ContinueWith(
                    t =>
                    {
                        if (!NetworkTaskSuccessful(t)) {
                            return;
                        }

                        _queue.DispatchAsync(OnSocketReady);
                    });
                NetworkStream = stream.AsStream();
            }
            else {
                NetworkStream = _client.GetStream();
                OnSocketReady();
            }
        }

        private void OnSocketReady()
        {
            var httpData = _logic.HTTPRequestData();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(IdleTimeout);
            NetworkStream.WriteAsync(httpData, 0, httpData.Length, cts.Token).ContinueWith(t =>
            {
                if (!NetworkTaskSuccessful(t)) {
                    return;
                }

                _queue.DispatchAsync(HandleHTTPResponse);
            }, cts.Token);
        }

        private async void HandleHTTPResponse()
        {
            Log.To.Sync.V(Tag, "WebSocket sent HTTP request...");
            
            using (var streamReader = new StreamReader(NetworkStream, Encoding.ASCII, false, 5, true)) {
                var parser = new HttpMessageParser(await streamReader.ReadLineAsync());
                while (true) {
                    var line = await streamReader.ReadLineAsync();
                    if (String.IsNullOrEmpty(line)) {
                        break;
                    }

                    parser.Append(line);
                }

                ReceivedHttpResponse(parser);
            }
        }

        private unsafe void ReceivedHttpResponse(HttpMessageParser parser)
        {
            _logic.ReceivedResponse(parser);
            var httpStatus = _logic.HttpStatus;

            if (_logic.ShouldRetry) {
                ResetConnections();
                Start();
                return;
            }

            var socket = _socket;
            _c4Queue.DispatchAsync(() =>
            {
                Dictionary<string, object> dict = parser.Headers.ToDictionary(x => x.Key, x => (object) x.Value);
                Native.c4socket_gotHTTPResponse(socket, (int) httpStatus, dict);
            });

            if (httpStatus != 101) {
                var closeCode = C4WebSocketCloseCode.WebSocketClosePolicyError;
                if (httpStatus >= 300 && httpStatus < 1000) {
                    closeCode = (C4WebSocketCloseCode)httpStatus;
                }

                var reason = parser.Reason;
                DidClose(closeCode, reason);
            } else if (!CheckHeader(parser, "Connection", "Upgrade", false)) {
                DidClose(C4WebSocketCloseCode.WebSocketCloseProtocolError, "Invalid 'Connection' header");
            } else if (!CheckHeader(parser, "Upgrade", "websocket", false)) {
                DidClose(C4WebSocketCloseCode.WebSocketCloseProtocolError, "Invalid 'Upgrade' header");
            } else if (!CheckHeader(parser, "Sec-WebSocket-Accept", _expectedAcceptHeader, true)) {
                DidClose(C4WebSocketCloseCode.WebSocketCloseProtocolError, "Invalid 'Sec-WebSocket-Accept' header");
            } else {
                Connected(parser);
            }
        }

        private unsafe void Connected(HttpMessageParser parser)
        {
            Log.To.Sync.I(Tag, "WebSocket CONNECTED!");
            Receive();
            var socket = _socket;
            _connected.Set();
            _c4Queue.DispatchAsync(() =>
            {
                Native.c4socket_opened(socket);
            });
        }

        private static bool CheckHeader(HttpMessageParser parser, string key, string expectedValue, bool caseSens)
        {
            string value;
            if (!parser.Headers.TryGetValue(key.ToLowerInvariant(), out value)) {
                return false;
            }

            var comparison = caseSens ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return value?.Equals(expectedValue, comparison) == true;
        }

        private bool NetworkTaskSuccessful(Task t)
        {
            if (t.IsCanceled) {
                DidClose(new SocketException((int)SocketError.TimedOut));
                return false;
            }

            if (t.Exception != null) {
                DidClose(t.Exception.Flatten().InnerException);
                return false;
            }

            return true;
        }

        private void ResetConnections()
        {
            _queue.DispatchAsync(() =>
            {
                _client?.Dispose();
                _client = null;
                NetworkStream?.Dispose();
                NetworkStream = null;
            });
        }

        private unsafe void DidClose(C4WebSocketCloseCode closeCode, string reason)
        {
            if (closeCode == C4WebSocketCloseCode.WebSocketCloseNormal) {
                DidClose(null);
                return;
            }

            if (NetworkStream == null) {
                return;
            }

            ResetConnections();

            Log.To.Sync.I(Tag, $"WebSocket CLOSED WITH STATUS {closeCode} \"{reason}\"");
            var c4Err = Native.c4error_make(C4ErrorDomain.WebSocketDomain, (int)closeCode, reason);
            _c4Queue.DispatchAsync(() => Native.c4socket_closed(_socket, c4Err));
        }

        private unsafe void DidClose(Exception e)
        {
            if (NetworkStream == null) {
                return;
            }

            ResetConnections();

            C4Error c4err;
            if (e != null && !(e is ObjectDisposedException) && !(e.InnerException is ObjectDisposedException)) {
                Log.To.Sync.I(Tag, $"WebSocket CLOSED WITH ERROR: {e}");
                Status.ConvertError(e, &c4err);
            } else {
                Log.To.Sync.I(Tag, "WebSocket CLOSED");
                c4err = new C4Error();
            }

            var c4errCopy = c4err;
            _c4Queue.DispatchAsync(() => Native.c4socket_closed(_socket, c4errCopy));
        }

        private unsafe void Receive()
        {
            _queue.DispatchAsync(() =>
            {
                if (_receiving || NetworkStream == null) {
                    return;
                }

                _receiving = true;
                _readMutex.WaitOne();
                var cts = new CancellationTokenSource();
                cts.CancelAfter(IdleTimeout);
                NetworkStream.ReadAsync(_buffer, 0, _buffer.Length, cts.Token)
                    .ContinueWith(t =>
                    {
                        _receiving = false;
                        if (!NetworkTaskSuccessful(t)) {
                            _readMutex.Set();
                            return;
                        }

                        _receivedBytesPending += (uint) t.Result;
                        Log.To.Sync.V(Tag, $"<<< received {t.Result} bytes [now {_receivedBytesPending} pending]");
                        var socket = _socket;
                        var data = _buffer.Take(t.Result).ToArray();
                        _readMutex.Set();
                        _c4Queue.DispatchAsync(() => Native.c4socket_received(socket, data));
                        if (_receivedBytesPending < MaxReceivedBytesPending) {
                            Receive();
                        }
                    }, cts.Token);
            });
        }

        private void SetupAuth()
        {
            var auth = _options?.Auth;
            if (auth != null) {
                var username = auth.Username;
                var password = auth.Password;
                if (username != null && password != null) {
                    _logic.Credential = new NetworkCredential(username, password);
                }
            }
        }

        #endregion
    }
}

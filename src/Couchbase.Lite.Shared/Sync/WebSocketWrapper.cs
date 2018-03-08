// 
// WebSocketWrapper.cs
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
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite.Sync
{
    internal sealed class WebSocketWrapper
    {
        #region Constants

        private const uint MaxReceivedBytesPending = 100 * 1024;
        private const string Tag = nameof(WebSocketWrapper);

        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(300);

        #endregion

        #region Variables

        [NotNull]private readonly byte[] _buffer = new byte[MaxReceivedBytesPending];
        [NotNull]private readonly SerialQueue _c4Queue = new SerialQueue();
        [NotNull]private readonly ManualResetEventSlim _connected = new ManualResetEventSlim();
        [NotNull]private readonly HTTPLogic _logic;
        [NotNull]private readonly ReplicatorOptionsDictionary _options;

        [NotNull]private readonly SerialQueue _queue = new SerialQueue();
        [NotNull]private readonly AutoResetEvent _readMutex = new AutoResetEvent(true);

        private readonly unsafe C4Socket* _socket;
        [NotNull]private readonly AutoResetEvent _writeMutex = new AutoResetEvent(true);
        private TcpClient _client;
        private bool _closed;
        private string _expectedAcceptHeader;
        private uint _receivedBytesPending;
        private bool _receiving;

        #endregion

        #region Properties

        public Stream NetworkStream { get; private set; }

        #endregion

        #region Constructors

        public unsafe WebSocketWrapper(Uri url, C4Socket* socket, [NotNull]ReplicatorOptionsDictionary options)
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
                _c4Queue.DispatchAsync(() =>
                {
                    if (_closed) {
                        Log.To.Sync.W(Tag, "Double close detected, ignoring...");
                        return;
                    }

                    Log.To.Sync.I(Tag, "Closing socket normally due to request from LiteCore");
                    Native.c4socket_closed(_socket, new C4Error(0, 0));
                    _closed = true;
                });
            });
        }

        public void CompletedReceive(ulong byteCount)
        {
            _queue.DispatchAsync(() =>
            {
                if (_closed) {
                    Log.To.Sync.V(Tag, "Already closed, ignoring call to CompletedReceive...");
                    return;
                }

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

                try {
                    // ReSharper disable once UseObjectOrCollectionInitializer
                    _client = new TcpClient(AddressFamily.InterNetworkV6)
                    {
                        SendTimeout = (int) IdleTimeout.TotalMilliseconds,
                        ReceiveTimeout = (int) IdleTimeout.TotalMilliseconds
                    };
                } catch (Exception e) {
                    DidClose(e);
                    return;
                }

                try {
                    _client.Client.DualMode = true;
                } catch(ArgumentException) {
                    Log.To.Sync.I(Tag, "IPv4/IPv6 dual mode not supported on this device, falling back to IPv4");
                    _client = new TcpClient(AddressFamily.InterNetwork)
                    {
                        SendTimeout = (int)IdleTimeout.TotalMilliseconds,
                        ReceiveTimeout = (int)IdleTimeout.TotalMilliseconds
                    };
                }

                var cts = new CancellationTokenSource();
                cts.CancelAfter(ConnectTimeout);
                _client.ConnectAsync(_logic.UrlRequest.Host, _logic.UrlRequest.Port).ContinueWith(t =>
                {
                    if (!NetworkTaskSuccessful(t)) {
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
                if (_closed) {
                    Log.To.Sync.W(Tag, "Already closed, ignoring call to Write...");
                    return;
                }

				_connected.Wait();
                _writeMutex.WaitOne();
                var cts = new CancellationTokenSource();
                cts.CancelAfter(IdleTimeout);
                if (NetworkStream == null) {
                    _writeMutex.Set();
                    Log.To.Sync.E(Tag, "Lost network stream, closing socket...");
                    DidClose(C4WebSocketCloseCode.WebSocketCloseAbnormal, "Unexpected error in client logic");
                    return;
                }

                NetworkStream.WriteAsync(data, 0, data.Length, cts.Token)
                    .ContinueWith(t =>
                    {
                        if (!NetworkTaskSuccessful(t)) {
                            _writeMutex.Set();
                            return;
                        }

                        _c4Queue.DispatchAsync(() =>
                        {
                            try {
                                if (!_closed) {
                                    Native.c4socket_completedWrite(_socket, (ulong) data.Length);
                                }
                            } finally {
                                _writeMutex.Set();
                            }
                        });
                    }, cts.Token);
	            });
        }

        #endregion

        #region Private Methods

        private static string Base64Digest(string input)
        {
            var data = Encoding.ASCII.GetBytes(input);
            var engine = SHA1.Create() ?? throw new RuntimeException("Failed to create SHA1 instance");
            var hashed = engine.ComputeHash(data);
            return Convert.ToBase64String(hashed);
        }

        private static bool CheckHeader([NotNull]HttpMessageParser parser, [NotNull]string key, string expectedValue, bool caseSens)
        {
            string value = null;
            if (parser.Headers?.TryGetValue(key, out value) != true) {
                return false;
            }

            var comparison = caseSens ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return value?.Equals(expectedValue, comparison) == true;
        }

        private unsafe void Connected()
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
            _c4Queue.DispatchAsync(() =>
            {
                if (_closed) {
                    Log.To.Sync.W(Tag, "Double close detected, ignoring...");
                    return;
                }

                Native.c4socket_closed(_socket, c4Err);
                _closed = true;
            });
        }

        private unsafe void DidClose(Exception e)
        {
            ResetConnections();

            C4Error c4err;
            if (e != null && !(e is ObjectDisposedException) && !(e.InnerException is ObjectDisposedException)) {
                Log.To.Sync.I(Tag, $"WebSocket CLOSED WITH ERROR: {e}");
                Status.ConvertNetworkError(e, &c4err);
            } else {
                Log.To.Sync.I(Tag, "WebSocket CLOSED");
                c4err = new C4Error();
            }

            var c4errCopy = c4err;
            _c4Queue.DispatchAsync(() =>
            {
                if (_closed) {
                    Log.To.Sync.W(Tag, "Double close detected, ignoring...");
                    return;
                }

                Native.c4socket_closed(_socket, c4errCopy);
                _closed = true;
            });
        }

        private async void HandleHTTPResponse()
        {
            Log.To.Sync.V(Tag, "WebSocket sent HTTP request...");
            try {
                using (var streamReader = new StreamReader(NetworkStream, Encoding.ASCII, false, 5, true)) {
                    var parser = new HttpMessageParser(await streamReader.ReadLineAsync().ConfigureAwait(false));
                    while (true) {

                        var line = await streamReader.ReadLineAsync().ConfigureAwait(false);
                        if (String.IsNullOrEmpty(line)) {
                            break;
                        }

                        parser.Append(line);

                    }

                    ReceivedHttpResponse(parser);
                }
            } catch (Exception e) {
                Log.To.Sync.I(Tag, "Error reading HTTP response of websocket handshake", e);
                DidClose(e);
            }
        }

        private bool NetworkTaskSuccessful(Task t)
        {
            if (t.IsCanceled) {
                DidClose(new SocketException((int)SocketError.TimedOut));
                return false;
            }

            if (t.Exception != null) {
                DidClose(t.Exception);
                return false;
            }

            return true;
        }

        private void OnSocketReady()
        {
            var httpData = _logic.HTTPRequestData();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(IdleTimeout);
            if (NetworkStream == null) {
                Log.To.Sync.E(Tag, "Socket reported ready, but no network stream available!");
                DidClose(C4WebSocketCloseCode.WebSocketCloseAbnormal, "Unexpected error in client logic");
                return;
            }

            NetworkStream.WriteAsync(httpData, 0, httpData.Length, cts.Token).ContinueWith(t =>
            {
                if (!NetworkTaskSuccessful(t)) {
                    return;
                }

                _queue.DispatchAsync(HandleHTTPResponse);
            }, cts.Token);
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
                        
                        _c4Queue.DispatchAsync(() =>
                        {
                            try {
                                // Guard against closure / disposal
                                if (!_closed) {
                                    Native.c4socket_received(socket, data);
                                    if (_receivedBytesPending < MaxReceivedBytesPending) {
                                        Receive();
                                    } else {
                                        Log.To.Sync.V(Tag, "Too much pending data, throttling Receive...");
                                    }
                                }
                            } finally {
                                _readMutex.Set();
                            }
                        });
                    }, cts.Token);
            });
        }

        private unsafe void ReceivedHttpResponse([NotNull]HttpMessageParser parser)
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
                var dict = parser.Headers?.ToDictionary(x => x.Key, x => (object) x.Value) ?? new Dictionary<string, object>();
                Native.c4socket_gotHTTPResponse(socket, httpStatus, dict);
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
                Connected();
            }
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

        private void StartInternal()
        {
            Log.To.Sync.I(Tag, $"WebSocket connecting to {_logic.UrlRequest?.Host}:{_logic.UrlRequest?.Port}");
            var rng = RandomNumberGenerator.Create() ?? throw new RuntimeException("Failed to create RandomNumberGenerator");
            var nonceBytes = new byte[16];
            rng.GetBytes(nonceBytes);
            var nonceKey = Convert.ToBase64String(nonceBytes);
            _expectedAcceptHeader = Base64Digest(String.Concat(nonceKey, "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));

            foreach (var header in _options.Headers) {
                _logic[header.Key] = header.Value;
            }

            var cookieString = _options.CookieString;
            if (cookieString != null) {
                // https://github.com/couchbase/couchbase-lite-net/issues/974
                // Don't overwrite a possible entry in the above headers unless there is
                // actually a value
                _logic["Cookie"] = cookieString;
            }

            // These ones should be overwritten.  The user has no business setting them.
            _logic["Connection"] = "Upgrade";
            _logic["Upgrade"] = "websocket";
            _logic["Sec-WebSocket-Version"] = "13";
            _logic["Sec-WebSocket-Key"] = nonceKey;
            var protocols = _options.Protocols;
            if (protocols != null) {
                _logic["Sec-WebSocket-Protocol"] = protocols;
            }
 
            if (_logic.UseTls) {
                var baseStream = _client?.GetStream();
                if (baseStream == null) {
                    Log.To.Sync.W(Tag, "Failed to get network stream (already closed?).  Aborting start...");
                    DidClose(C4WebSocketCloseCode.WebSocketCloseAbnormal, "Unexpected error in client logic");
                    return;
                }

                var stream = new SslStream(baseStream, false, ValidateServerCert);
                X509CertificateCollection clientCerts = null;
                if (_options.ClientCert != null) {
                    clientCerts = new X509CertificateCollection(new[] {_options.ClientCert as X509Certificate});
                }
                
                stream.AuthenticateAsClientAsync(_logic.UrlRequest?.Host, clientCerts, SslProtocols.Tls12, false).ContinueWith(
                    t =>
                    {
                        if (!NetworkTaskSuccessful(t)) {
                            return;
                        }

                        _queue.DispatchAsync(OnSocketReady);
                    });
                NetworkStream = stream;
            }
            else {
                NetworkStream = _client?.GetStream();
                OnSocketReady();
            }
        }

        private bool ValidateServerCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_options.PinnedServerCertificate != null) {
                var retVal = certificate.Equals(_options.PinnedServerCertificate);
                if (!retVal) {
                    Log.To.Sync.W(Tag, "Server certificate did not match the pinned one!");
                }

                return retVal;
            }

            if (sslPolicyErrors != SslPolicyErrors.None) {
                Log.To.Sync.W(Tag, $"Error validating TLS chain: {sslPolicyErrors}");
                if (chain?.ChainStatus != null) {
                    for (var i = 0; i < chain.ChainStatus.Length; i++) {
                        var element = chain.ChainElements[i];
                        var status = chain.ChainStatus[i];
                        if (status.Status != X509ChainStatusFlags.NoError) {
                            Log.To.Sync.V(Tag,
                                $"Error {status.Status} ({status.StatusInformation}) for certificate:{Environment.NewLine}{element.Certificate}");
                        }
                    }
                }
            }

            return sslPolicyErrors == SslPolicyErrors.None;
        }

        #endregion
    }
}

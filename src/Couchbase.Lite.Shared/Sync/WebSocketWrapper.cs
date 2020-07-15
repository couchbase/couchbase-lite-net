﻿// 
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
using System.Collections.Concurrent;
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

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite.Sync
{
    // This class is the workhorse of the flow of data during replication and needs
    // to be airtight.  Some notes:  The network stream absolutely must not be written
    // to and read from at the same time.  The documentation mentions two unique threads,
    // one for read and one for write, as the best approach.  
    //
    // This class implements an actor like system to try to avoid locking and backing
    // things up.  There are three distinct areas:
    //
    // <c>_queue</c>: This is a serial queue that processes actions that involve the state
    // of the C4Socket* object (receiving data, writing data, closing, opening, etc)
    //
    // <c>PerformRead</c>: Dedicated thread for reading from the remote stream, and passing
    // what it finds to the <c>_queue</c>
    //
    // <c>PerformWrite</c>: Dedicated thread for writing to the remote stream.  It receives
    // data via a pub-sub system whereby it is the subscriber. 
    //
    // <c>_c4Queue</c>: This is a serial queue that process actions that involve operating
    // on the C4Socket* object at the native (LiteCore) level.  The queue is used for thread
    // safety.
    //
    // Example: When it is time to write, LiteCore will callback into the C# callback, and
    // the data will end up in the <c>Write</c> method.  This method will enter the queue,
    // and then publish a message to the write thread (order is important) before exiting
    // the queue.  The write thread will pick that message up, send it, and then enter the
    // _c4Queue to inform LiteCore that it finished sending the data.
    internal sealed class WebSocketWrapper
    {
        #region Constants

        private const uint MaxReceivedBytesPending = 100 * 1024;
        private const string Tag = nameof(WebSocketWrapper);


        #endregion

        #region Variables

        [NotNull]private readonly byte[] _buffer = new byte[MaxReceivedBytesPending];
        [NotNull]private readonly SerialQueue _c4Queue = new SerialQueue();
        [NotNull]private readonly HTTPLogic _logic;
        [NotNull]private readonly ReplicatorOptionsDictionary _options;

        [NotNull]private readonly SerialQueue _queue = new SerialQueue();

        private readonly unsafe C4Socket* _socket;
        private TcpClient _client;
        private bool _closed;
        private string _expectedAcceptHeader;
        private CancellationTokenSource _readWriteCancellationTokenSource;
        private uint _receivedBytesPending;
        private ManualResetEventSlim _receivePause;
        private BlockingCollection<byte[]> _writeQueue;
        private readonly object _writeQueueLock = new object(); // Used to avoid disposal race
        
        private readonly IReachability _reachability = Service.GetInstance<IReachability>() ?? new Reachability();

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
            _reachability.StatusChanged += ReachabilityChanged;
            _reachability.Url = url;
            _reachability.Start();

            SetupAuth();
        }

        #endregion

        #region Public Methods

        // Normal closure (requested by client)
        public unsafe void CloseSocket()
		{
            // Wait my turn!
            _queue.DispatchAsync(() =>
            {
                ResetConnections();
                _c4Queue.DispatchAsync(() =>
                {
                    if (_closed) {
                        WriteLog.To.Sync.W(Tag, "Double close detected, ignoring...");
                        return;
                    }

                    WriteLog.To.Sync.I(Tag, "Closing socket normally due to request from LiteCore");
                    Native.c4socket_closed(_socket, new C4Error(0, 0));
                    _closed = true;
                });
            });
        }

        // LiteCore finished processing X number of bytes
        public void CompletedReceive(ulong byteCount)
        {
            _queue.DispatchAsync(() =>
            {
                if (_closed) {
                    WriteLog.To.Sync.V(Tag, "Already closed, ignoring call to CompletedReceive...");
                    return;
                }

                _receivedBytesPending -= (uint)byteCount;
                _receivePause?.Set();
            });
        }

        // This starts the flow of data, and it is quite an intense multi step process
        // So I will label it in sequential order
        public void Start()
        {
            _queue.DispatchAsync(() =>
            {
                if (_client != null) {
                    WriteLog.To.Sync.W(Tag, "Ignoring duplicate call to Start...");
                    return;
                }

                _readWriteCancellationTokenSource = new CancellationTokenSource();
                _writeQueue = new BlockingCollection<byte[]>();
                _receivePause = new ManualResetEventSlim(true);

                // STEP 1: Create the TcpClient, which is responsible for negotiating
                // the socket connection between here and the server
                try {
                    // ReSharper disable once UseObjectOrCollectionInitializer
                    _client = new TcpClient(AddressFamily.InterNetworkV6);
                } catch (Exception e) {
                    DidClose(e);
                    return;
                }

                try {
                    _client.Client.DualMode = true;
                } catch(ArgumentException) {
                    WriteLog.To.Sync.I(Tag, "IPv4/IPv6 dual mode not supported on this device, falling back to IPv4");
                    _client = new TcpClient(AddressFamily.InterNetwork);
                }

                // STEP 2.5: The IProxy interface will detect a system wide proxy that is set
                // And if it is, it will return an IWebProxy object to use
                // Sending "CONNECT" request if IWebProxy object is not null
                IProxy proxy = Service.GetInstance<IProxy>();

                try {
                    if (_client != null && !_client.Connected) {
                        if (proxy != null) {
                            connectProxyAsync(proxy, "proxyUser", "proxyPassword");
                        }
                    }
                } catch { }

                if (proxy == null) {
                    OpenConnectionToRemote();
                }
            });
        }

        public void Write(byte[] data)
        {
            try {
                _writeQueue?.Add(data);
            } catch (InvalidOperationException) {
                WriteLog.To.Sync.I(Tag, "Attempt to write after closing socket, ignore...");
            }
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
            // STEP 8: We have web socket connectivity!  Start the read and write threads.
            WriteLog.To.Sync.I(Tag, "WebSocket CONNECTED!");
            var socket = _socket;
            _c4Queue.DispatchAsync(() =>
            {
                Native.c4socket_opened(socket);
                Task.Factory.StartNew(PerformWrite);
                Task.Factory.StartNew(PerformRead);
            });
        }

        private async void connectProxyAsync(IProxy proxy, string user, string password)
        {
            try {
                Uri destinationUri = new Uri("http://"+_logic.UrlRequest.Host + ":" + _logic.UrlRequest.Port);
                var proxyServer = await proxy.CreateProxyAsync(destinationUri);
                if (proxyServer == null) {
                    OpenConnectionToRemote();
                    return;
                }

                _logic.HasProxy = true;
                //create remote endpoint
                IPAddress add = IPAddress.Parse(proxyServer.Address.Host);
                //connect remote proxy endpoint
                await _client.ConnectAsync(add, proxyServer.Address.Port).ConfigureAwait(false);
                NetworkStream = _client.GetStream();
                var proxyRequest = _logic.ProxyRequest();
                NetworkStream.Write(proxyRequest, 0, proxyRequest.Length);
                await WaitForResponse(NetworkStream).ConfigureAwait(false);
            } catch (Exception E) {
                Console.WriteLine(E.Message);
            }
        }

        private unsafe void DidClose(C4WebSocketCloseCode closeCode, string reason)
        {
            if (closeCode == C4WebSocketCloseCode.WebSocketCloseNormal) {
                DidClose(null);
                return;
            }

            ResetConnections();

            WriteLog.To.Sync.I(Tag, $"WebSocket CLOSED WITH STATUS {closeCode} \"{reason}\"");
            var c4Err = Native.c4error_make(C4ErrorDomain.WebSocketDomain, (int)closeCode, reason);
            _c4Queue.DispatchAsync(() =>
            {
                if (_closed) {
                    WriteLog.To.Sync.W(Tag, "Double close detected, ignoring...");
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
                WriteLog.To.Sync.I(Tag, $"WebSocket CLOSED WITH ERROR: {e}");
                Status.ConvertNetworkError(e, &c4err);
            } else {
                WriteLog.To.Sync.I(Tag, "WebSocket CLOSED");
                c4err = new C4Error();
            }

            var c4errCopy = c4err;
            _c4Queue.DispatchAsync(() =>
            {
                if (_closed) {
                    WriteLog.To.Sync.W(Tag, "Double close detected, ignoring...");
                    return;
                }
                
                Native.c4socket_closed(_socket, c4errCopy);
                _closed = true;
            });
        }

        private async void HandleHTTPResponse()
        {
            // STEP 6: Read and parse the HTTP response
            WriteLog.To.Sync.V(Tag, "WebSocket sent HTTP request...");
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
                WriteLog.To.Sync.I(Tag, "Error reading HTTP response of websocket handshake", e);
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
            //STEP 5: Send the HTTP request to start the WebSocket upgrade
            var httpData = _logic.HTTPRequestData();
            if (NetworkStream == null) {
                WriteLog.To.Sync.E(Tag, "Socket reported ready, but no network stream available!");
                DidClose(C4WebSocketCloseCode.WebSocketCloseAbnormal, "Unexpected error in client logic");
                return;
            }

            NetworkStream.WriteAsync(httpData, 0, httpData.Length).ContinueWith(t =>
            {
                if (!NetworkTaskSuccessful(t)) {
                    return;
                }

                _queue.DispatchAsync(HandleHTTPResponse);
            });
        }

        private void OpenConnectionToRemote()
        {
            // STEP 2: Open the socket connection to the remote host

            if(_logic.HasProxy) {
                _queue.DispatchAsync(StartInternal);
            }
            else if (_client != null && !_client.Connected) {
                try {
                    _client.ConnectAsync(_logic.UrlRequest.Host, _logic.UrlRequest.Port)
                    .ContinueWith(t =>
                    {
                        if (t.Exception != null && t.Exception.InnerExceptions.Any(x => x is NullReferenceException)) {
                            WriteLog.To.Sync.I(Tag,
                                "Ignoring bug in .NET runtime (NRE when closing TcpClient before connection is made");
                            return;
                        }

                        if (!NetworkTaskSuccessful(t)) {
                            return;
                        }
                        _queue.DispatchAsync(StartInternal);

                    });
                } catch (Exception e) {
                    // Yes, unfortunately exceptions can either be thrown here or in the task...
                    DidClose(e);
                }
            }
        }

        // Run in a dedicated thread
        private async void PerformRead()
        {
            var original = _readWriteCancellationTokenSource;
            if (original == null) {
                WriteLog.To.Sync.V(Tag, "_readWriteCancellationTokenSource is null, cancelling read...");
                return;
            }

            var zeroByteCount = 0;
            // This will protect us against future nullification of the original source
            var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(original.Token);
            while (!cancelSource.IsCancellationRequested) {
                try {
                    _receivePause?.Wait(cancelSource.Token);
                } catch (ObjectDisposedException) {
                    return;
                } catch(OperationCanceledException){
                    return;
                }
                
                try {
                    var stream = NetworkStream;
                    if (stream == null) {
                        return;
                    }

                    // Phew, at this point we are clear to actually read from the stream
                    var received = await stream.ReadAsync(_buffer, 0, _buffer.Length, cancelSource.Token).ConfigureAwait(false);
                    if (received == 0) {
                        if (zeroByteCount++ >= 10) {
                            WriteLog.To.Sync.I(Tag, "Failed to read from stream too many times, signaling closed...");
                            DidClose(new CouchbasePosixException(PosixBase.GetCode(nameof(PosixWindows.ECONNRESET))));
                            return;
                        }

                        // Should only happen on a closed stream, but just in case let's continue
                        // after a small delay (wait for cancellation to confirm)
                        Thread.Sleep(200);
                        continue;
                    }

                    zeroByteCount = 0;
                    var data = _buffer.Take(received).ToArray();
                    Receive(data);
                } catch (Exception e) {
                    DidClose(e);
                    return;
                }
            }
        }

        private async void PerformWrite()
        {
            var original = _readWriteCancellationTokenSource;
            if (original == null) {
                return;
            }
            
            // This will protect us against future nullification of the original source
            var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(original.Token);
            while (!cancelSource.IsCancellationRequested) {
                try {
                    var completed = _writeQueue == null || _writeQueue?.IsCompleted == true;
                    if (completed) {
                        return;
                    }

                    byte[] nextData;
                    lock (_writeQueueLock) {
                        nextData = _writeQueue?.Take(cancelSource.Token);
                    }

                    if (nextData == null) {
                        return; // write queue is already gone
                    }

                    try {
                        var stream = NetworkStream;
                        if (stream == null) {
                            return;
                        }

                        // Clear to try to write
                        await stream.WriteAsync(nextData, 0, nextData.Length, cancelSource.Token).ConfigureAwait(false);
                    } catch (Exception e) {
                        DidClose(e);
                        return;
                    }

                    var silenceCompiler = _c4Queue.DispatchAsync(() =>
                    {
                        if (!_closed) {
                            unsafe {
                                Native.c4socket_completedWrite(_socket, (ulong) nextData.Length);
                            }
                        }
                    });
                } catch (OperationCanceledException) {
                    return;
                } catch (ArgumentNullException) {
                    return; // Sometimes happens because of Dispose() call to _writeQueue, safe to ignore
                } catch (NullReferenceException) {
                    return; // Sometimes happens because of Dispose() call to _writeQueue, safe to ignore
                }
            }
        }

        private void ReachabilityChanged(object sender, NetworkReachabilityChangeEventArgs e)
        {
            _queue.DispatchAsync(() =>
            {
                if(NetworkStream != null && e.Status == NetworkReachabilityStatus.Unreachable) {
                    DidClose(new SocketException((int)SocketError.NetworkUnreachable));
                }
            });
        }

        private unsafe void Receive(byte[] data)
        {
            // Schedule the processing to happen on the queue.  Out of order
            // messages cause checksum errors!
            _queue.DispatchAsync(() =>
            {
                _receivedBytesPending += (uint)data.Length;
                WriteLog.To.Sync.V(Tag, $"<<< received {data.Length} bytes [now {_receivedBytesPending} pending]");
                var socket = _socket;
                _c4Queue.DispatchAsync(() =>
                {
                    // Guard against closure / disposal
                    if (!_closed) {
                        Native.c4socket_received(socket, data);
                        if (_receivedBytesPending >= MaxReceivedBytesPending) {
                            WriteLog.To.Sync.V(Tag, "Too much pending data, throttling Receive...");
                            _receivePause?.Reset();
                        }
                    }
                });
            });
        }

        private unsafe void ReceivedHttpResponse([NotNull]HttpMessageParser parser)
        {
            // STEP 7: Determine if the HTTP response was a success
            _logic.ReceivedResponse(parser);
            var httpStatus = _logic.HttpStatus;

            if (_logic.ShouldRetry) {
                // Usually authentication needed, or a redirect
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

            // Success is a 101 response, anything else is not good
            if (_logic.Error != null) {
                DidClose(_logic.Error);
            } else if (httpStatus != 101) {
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
                _readWriteCancellationTokenSource?.Cancel();
                _readWriteCancellationTokenSource?.Dispose();
                _readWriteCancellationTokenSource = null;
                _receivePause?.Dispose();
                _receivePause = null;
                _writeQueue?.CompleteAdding();
                var count = 0;
                while (count++ < 5 && _writeQueue != null && !_writeQueue.IsCompleted) {
                    Thread.Sleep(500);
                }

                if (_writeQueue != null && !_writeQueue.IsCompleted) {
                    WriteLog.To.Sync.W(Tag, "Timed out waiting for _writeQueue to finish, forcing Dispose...");
                }

                lock (_writeQueueLock) {
                    Misc.SafeSwap(ref _writeQueue, null);
                }
            });
        }

        private void SetupAuth()
        {
            var auth = _options?.Auth;
            if (auth != null) {
                if (auth.Type == AuthType.HttpBasic) {
                    var username = auth.Username;

                    // TODO string Password will be deprecated and replaced with byte array password
                    var password = auth.PasswordData != null ?
                         Encoding.Unicode.GetString(auth.PasswordData)
                         : auth.Password;

                    if (username != null && password != null) {
                        _logic.Credential = new NetworkCredential(username, password);
                    }
                }
            }
        }

        private void StartInternal()
        {
            // STEP 3: Create the WebSocket Upgrade HTTP request
            WriteLog.To.Sync.I(Tag, $"WebSocket connecting to {_logic.UrlRequest?.Host}:{_logic.UrlRequest?.Port}");
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
                    WriteLog.To.Sync.W(Tag, "Failed to get network stream (already closed?).  Aborting start...");
                    DidClose(C4WebSocketCloseCode.WebSocketCloseAbnormal, "Unexpected error in client logic");
                    return;
                }

                var stream = new SslStream(baseStream, false, ValidateServerCert);
                X509CertificateCollection clientCerts = null;
                if (_options.ClientCert != null) {
                    clientCerts = new X509CertificateCollection(new[] { _options.ClientCert as X509Certificate });
                }

                // STEP 3A: TLS handshake
                stream.AuthenticateAsClientAsync(_logic.UrlRequest?.Host, clientCerts, SslProtocols.Tls12, false).ContinueWith(
                    t =>
                    {
                        if (!NetworkTaskSuccessful(t)) {
                            return;
                        }

                        _queue.DispatchAsync(OnSocketReady);
                    });
                NetworkStream = stream;
            } else {
                NetworkStream = _client?.GetStream();
                OnSocketReady();
            }
        }

        private bool ValidateServerCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_options.PinnedServerCertificate != null) {
                var retVal = certificate.Equals(_options.PinnedServerCertificate);
                if (!retVal) {
                    WriteLog.To.Sync.W(Tag, "Server certificate did not match the pinned one!");
                }

                return retVal;
            }

            if (sslPolicyErrors != SslPolicyErrors.None) {
                WriteLog.To.Sync.W(Tag, $"Error validating TLS chain: {sslPolicyErrors}");
                if (chain?.ChainStatus != null) {
                    for (var i = 0; i < chain.ChainStatus.Length; i++) {
                        var element = chain.ChainElements[i];
                        var status = chain.ChainStatus[i];
                        if (status.Status != X509ChainStatusFlags.NoError) {
                            WriteLog.To.Sync.V(Tag,
                                $"Error {status.Status} ({status.StatusInformation}) for certificate:{Environment.NewLine}{element.Certificate}");
                        }
                    }
                }
            }

            return sslPolicyErrors == SslPolicyErrors.None;
        }

        private async Task WaitForResponse(Stream stream)
        {
            await Task.Factory.StartNew(() => {
                byte[] buffer = new byte[16384];
                int responseLength = stream.Read(buffer, 0, buffer.Length);
                string resp = System.Text.UTF8Encoding.UTF8.GetString(buffer, 0, responseLength);
                if (resp.Contains("200")) {
                    OpenConnectionToRemote();
                }
            });
        }

        #endregion
    }
}


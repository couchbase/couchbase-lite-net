// 
//  MockConnection.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

#if COUCHBASE_ENTERPRISE
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Internal.P2P;
using Couchbase.Lite.P2P;

namespace Couchbase.Lite;

public abstract class MockConnection(MessageEndpointListener? host, ProtocolType protocolType)
    : IMessageEndpointConnection
{
    protected readonly MessageEndpointListener? _host = host;
    protected readonly ProtocolType _protocolType = protocolType;

    protected IReplicatorConnection? _connection;
    private StreamWriter? _logOut;
    protected bool _noCloseRequest;

    public IMockConnectionErrorLogic ErrorLogic { get; set; } = new NoErrorLogic();

    private bool IsClient => _host == null;
        
    public void Accept(byte[] message)
    {
        if (IsClient && ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Receive)) {
            var e = ErrorLogic.CreateException();
            ConnectionBroken(e);
            _connection?.Close(e);
        } else {
            _connection?.Receive(Message.FromBytes(message));
        }
    }

    public void LogConversation(string logPath)
    {
        var dirName = Path.GetDirectoryName(logPath);
        Debug.Assert(dirName != null, "dirName != null");
        if (!Directory.Exists(dirName)) {
            Directory.CreateDirectory(dirName);
        }

        var outHandle = File.Open(logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _logOut = new StreamWriter(outHandle, Encoding.UTF8)
        {
            AutoFlush = true
        };

        var start = $"{Environment.NewLine}Starting new session{Environment.NewLine}";
        _logOut.WriteLine(start);
    }

    protected abstract void ConnectionBroken(MessagingException? exception);

    protected abstract void PerformWrite(byte[] message);

    public virtual Task Close(Exception? error)
    {
        return Task.CompletedTask;
    }

    public virtual Task Open(IReplicatorConnection connection)
    {
        _connection = connection;
        if (!IsClient || !ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Connect)) {
            return Task.CompletedTask;
        }
            
        var e = ErrorLogic.CreateException();
        Debug.Assert(e != null, "e != null");
        ConnectionBroken(e);
        return Task.FromException(e);

    }

    public Task Send(Message message)
    {
        if (IsClient && ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Send)) {
            var e = ErrorLogic.CreateException();
            Debug.Assert(e != null, "e != null");
            ConnectionBroken(e);
            return Task.FromException(e);
        }

        var data = message.ToByteArray();
        PerformWrite(data);
        return Task.CompletedTask;
    }

    private sealed class NoErrorLogic : IMockConnectionErrorLogic
    {
        public MessagingException? CreateException() => null;

        public bool ShouldClose(MockConnectionLifecycleLocation location) => false;
    }
}

public sealed class MockClientConnection(MessageEndpoint endpoint) : MockConnection(null, endpoint.ProtocolType)
{
    private MockServerConnection? _server = endpoint.Target as MockServerConnection;
    private readonly ManualResetEventSlim _serverWait = new();
    private const string Tag = nameof(MockClientConnection);

    protected override void ConnectionBroken(MessagingException? exception)
    {
        var server = _server;
        _server = null;
        server?.ClientDisconnected(exception);
    }

    public override async Task Open(IReplicatorConnection connection)
    {
        await base.Open(connection).ConfigureAwait(false);
        _server?.ClientOpened(this);
        while(!_serverWait.Wait(TimeSpan.FromSeconds(1))) {
            WriteLog.To.Sync.W(Tag, "Connection appears stuck waiting for server");
        }
    }

    public override Task Close(Exception? error)
    {
        if (_connection == null) {
            return Task.CompletedTask;
        }

        if (ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Close)) {
            var e = ErrorLogic.CreateException();
            ConnectionBroken(e);
            Debug.Assert(e != null, "e != null");
            return Task.FromException(e);
        }
            
        _connection = null;

        if (_protocolType == ProtocolType.MessageStream && !_noCloseRequest) {
            ConnectionBroken(null);
        }

        return Task.CompletedTask;
    }

    public void ServerConnected()
    {
        _serverWait.Set();
    }

    public void ServerDisconnected()
    {
        _server = null;
        _noCloseRequest = true;
        _connection?.Close(null);
    }

    protected override void PerformWrite(byte[] message)
    {
        _server?.Accept(message);
    }
}

public sealed class MockServerConnection(MessageEndpointListener host, ProtocolType protocolType)
    : MockConnection(host, protocolType)
{
    private MockClientConnection? _client;

    public void ClientOpened(MockClientConnection client)
    {
        _client = client;
        _host?.Accept(this);
    }

    protected override void ConnectionBroken(MessagingException? exception)
    {
        // No-op
    }

    public override Task Close(Exception? e)
    {
        _host?.Close(this);
        if (_protocolType == ProtocolType.MessageStream && e == null) {
            _client?.ServerDisconnected();
        }

        return Task.CompletedTask;
    }

    public override async Task Open(IReplicatorConnection connection)
    {
        await base.Open(connection).ConfigureAwait(false);

        // HACK: Sorry, I hope this is only needed for this mock
        if (connection is not CouchbaseSocket cbSocket) {
            throw new InvalidOperationException("Expected CouchbaseSocket");
        }

        if (_client == null) {
            throw new InvalidOperationException("_client == null");
        }

        cbSocket.OpenCompletion = _client.ServerConnected;
    }

    protected override void PerformWrite(byte[] message)
    {
        _client?.Accept(message);
    }

    public void ClientDisconnected(MessagingException? exception)
    {
        _noCloseRequest = true;
        _connection?.Close(exception);
    }
}
#endif
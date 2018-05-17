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

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite.P2P;

namespace Couchbase.Lite
{
    public abstract class MockConnection : IMessageEndpointConnection
    {
        #region Variables

        protected readonly MessageEndpointListener _host;
        private readonly ProtocolType _protocolType;

        protected IReplicatorConnection _connection;
        private IMockConnectionErrorLogic _errorLogic;
        private StreamWriter _logOut;
        protected bool _noCloseRequest;

        #endregion

        #region Properties

        public IMockConnectionErrorLogic ErrorLogic
        {
            get => _errorLogic ?? (_errorLogic = new NoErrorLogic());
            set => _errorLogic = value;
        }

        #endregion

        #region Constructors

        protected MockConnection(Database database, bool outgoing, ProtocolType protocolType)
        {
            if (!outgoing) {
                _host = new MessageEndpointListener(
                    new MessageEndpointListenerConfiguration(database, protocolType));
            }

            _protocolType = protocolType;
        }

        #endregion

        #region Public Methods

        public void Accept(byte[] message)
        {
            if (ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Receive)) {
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

        #endregion

        #region Protected Methods

        protected abstract void ConnectionBroken(MessagingException exception);

        protected abstract void PerformWrite(byte[] message);

        #endregion

        #region Private Methods

        private void Log(string message)
        {
            _logOut?.WriteLine(message);
        }

        #endregion

        #region IMessageEndpointConnection

        public Task Close(Exception error)
        {
            if (_connection == null) {
                return Task.CompletedTask;
            }

            if (ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Close)) {
                var e = ErrorLogic.CreateException();
                ConnectionBroken(e);
                return Task.FromException(e);
            }

            _host?.Close(this);
            _connection = null;

            if (_protocolType == ProtocolType.MessageStream && !_noCloseRequest) {
                ConnectionBroken(null);
            }

            return Task.CompletedTask;
        }

        public virtual Task Open(IReplicatorConnection connection)
        {
            _connection = connection;
            if (ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Connect)) {
                var e = ErrorLogic.CreateException();
                ConnectionBroken(e);
                return Task.FromException(e);
            }

            return Task.CompletedTask;
        }

        public Task Send(Message message)
        {
            if (ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Send)) {
                var e = ErrorLogic.CreateException();
                ConnectionBroken(e);
                return Task.FromException(e);
            }

            var data = message.ToByteArray();
            PerformWrite(data);
            return Task.CompletedTask;
        }

        #endregion

        #region Nested

        private sealed class NoErrorLogic : IMockConnectionErrorLogic
        {
            #region IMockConnectionErrorLogic

            public MessagingException CreateException()
            {
                return null;
            }

            public bool ShouldClose(MockConnectionLifecycleLocation location)
            {
                return false;
            }

            #endregion
        }

        #endregion
    }

    public sealed class MockClientConnection : MockConnection
    {
        #region Variables

        private MockServerConnection _server;

        #endregion

        #region Constructors

        public MockClientConnection(Database db, MockServerConnection server, ProtocolType protocolType)
            : base(db, true, protocolType)
        {
            _server = server;
        }

        #endregion

        #region Overrides

        protected override void ConnectionBroken(MessagingException exception)
        {
            var server = _server;
            _server = null;
            server?.ClientDisconnected(exception);
        }

        public override async Task Open(IReplicatorConnection connection)
        {
            await base.Open(connection).ConfigureAwait(false);
            _server?.ClientOpened(this);
        }

        protected override void PerformWrite(byte[] message)
        {
            _server?.Accept(message);
        }

        #endregion

        public void ServerDisconnected(MessagingException exception)
        {
            _noCloseRequest = true;
            _connection?.Close(exception);
        }
    }

    public sealed class MockServerConnection : MockConnection
    {
        #region Variables

        private MockClientConnection _client;

        #endregion

        #region Constructors

        public MockServerConnection(Database db, ProtocolType protocolType)
            : base(db, false, protocolType)
        {
        }

        #endregion

        #region Public Methods

        public void ClientOpened(MockClientConnection client)
        {
            _client = client;
            _host.Accept(this);
        }

        #endregion

        #region Overrides

        protected override void ConnectionBroken(MessagingException exception)
        {
            var client = _client;
            _client = null;
            client?.ServerDisconnected(exception);
        }

        protected override void PerformWrite(byte[] message)
        {
            _client?.Accept(message);
        }

        #endregion

        public void ClientDisconnected(MessagingException exception)
        {
            _noCloseRequest = true;
            _connection?.Close(exception);
        }
    }
}
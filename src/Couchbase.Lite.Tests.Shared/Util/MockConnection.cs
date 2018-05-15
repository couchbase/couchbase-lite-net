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
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.P2P;

using Newtonsoft.Json;

namespace Couchbase.Lite
{
    public abstract class MockConnection : IMessageEndpointConnection
    {
        #region Constants

        protected const char CommandPrefix = '!';
        protected const char MessagePrefix = '\0';

        #endregion

        #region Variables

        protected readonly MessageEndpointListener _host;
        
        protected IReplicatorConnection _connection;
        private bool _receivedClose;
        private IMockConnectionErrorLogic _errorLogic;

        #endregion

        private IMockConnectionErrorLogic ErrorLogic => _errorLogic ?? (_errorLogic = new NoErrorLogic());

        private MockConnectionType ConnectionType =>
            _host == null ? MockConnectionType.Client : MockConnectionType.Server;

        #region Constructors

        protected MockConnection(Database database, bool outgoing)
        {
            if (!outgoing) {
                _host = new MessageEndpointListener(
                    new MessageEndpointListenerConfiguration(database, ProtocolType.ByteStream));
            }
        }

        #endregion

        #region Public Methods

        public void Accept(byte[] message)
        {
            if (ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Receive, ConnectionType)) {
                _connection?.Close(ErrorLogic.CreateException(MockConnectionLifecycleLocation.Receive, ConnectionType));
            } else {
                _connection?.Receive(Message.FromBytes(message));
            }
        }

        #endregion

        #region Protected Methods

        protected abstract void PerformWrite(byte[] message);

        #endregion

        #region IMessageEndpointConnection

        public Task Close(Exception error)
        {
            if (_connection == null) {
                return Task.CompletedTask;
            }

            _host?.Close(this);
            _connection = null;

            return Task.CompletedTask;
        }

        public virtual Task Open(IReplicatorConnection connection)
        {
            _connection = connection;
            if (ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Connect, ConnectionType)) {
                return Task.FromException(ErrorLogic.CreateException(MockConnectionLifecycleLocation.Connect, ConnectionType));
            }

            return Task.CompletedTask;
        }

        public Task Send(Message message)
        {
            if (ErrorLogic.ShouldClose(MockConnectionLifecycleLocation.Send, ConnectionType)) {
                return Task.FromException(ErrorLogic.CreateException(MockConnectionLifecycleLocation.Send,
                    ConnectionType));
            }

            var data = message.ToByteArray();
            PerformWrite(data);
            return Task.CompletedTask;
        }

        #endregion

        private sealed class NoErrorLogic : IMockConnectionErrorLogic
        {
            public bool ShouldClose(MockConnectionLifecycleLocation location, MockConnectionType connectionType)
            {
                return false;
            }

            public MessagingErrorException CreateException(MockConnectionLifecycleLocation location, MockConnectionType connectionType)
            {
                return null;
            }
        }
    }

    public sealed class MockClientConnection : MockConnection
    {
        #region Variables

        private readonly MockServerConnection _server;

        #endregion

        #region Constructors

        public MockClientConnection(Database db, MockServerConnection server)
            : base(db, true)
        {
            _server = server;
        }

        #endregion

        #region Overrides

        public override Task Open(IReplicatorConnection connection)
        {
            return base.Open(connection).ContinueWith(t =>
            {
                _server.ClientOpened(this);
            });
        }

        protected override void PerformWrite(byte[] message)
        {
            _server.Accept(message);
        }

        #endregion

    }

    public sealed class MockServerConnection : MockConnection
    {
        #region Variables

        private MockClientConnection _client;

        #endregion

        #region Constructors

        public MockServerConnection(Database db)
            : base(db, false)
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

        protected override void PerformWrite(byte[] message)
        {
            _client.Accept(message);
        }

        #endregion
    }
}
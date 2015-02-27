//
//  Broadcaster.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Couchbase.Lite.Util;
using System.Text;

namespace Couchbase.Lite.PeerToPeer
{
    public class UDPBroadcasting
    {
        private const string TAG = "Listener";
        private static readonly IPAddress _McastAddress = IPAddress.Parse("224.168.100.2");

        private readonly Socket _mcastSocket;
        private readonly Thread _thread;
        private volatile bool _finished = false;



        public ushort Port { get; private set; }

        public UDPBroadcasting(Manager manager, ushort port)
        { 
            Port = port;
            _mcastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _mcastSocket.Bind();
            var mcastOption = new MulticastOption(_McastAddress, IPAddress.Any);
            _mcastSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, mcastOption);
            _thread = new Thread(Listen) {
                Name = "Broadcast Listener Thread",
                IsBackground = true
            };
            _thread.Start();

            Task.Factory.StartNew(DoHeartbeat);
        }

        private void DoHeartbeat()
        {

        }

        private void Listen()
        {
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any,0);
            IPEndPoint groupEP = new IPEndPoint(mcastAddress, mcastPort);
            byte[] buffer = new byte[1024];
            try {
                while (!_finished) {
                    Log.V(TAG, "    Waiting for broadcast...");
                    _mcastSocket.ReceiveFrom(buffer, ref remoteEP);

                    #if DEBUG
                    string message = Encoding.UTF8.GetString(bytes);
                    Log.V(TAG, "    Received broadcast from {0}:\n{1}", groupEP, message);
                    #endif

                    ProcessMessage(bytes);
                }
            } catch(Exception e) {
                Log.E(TAG, "Exception in broadcast listener thread", e);
            } finally {
                _mcastSocket.Close();
                _mcastSocket = null;
            }
        }

        private void ProcessMessage(byte[] message)
        {

        }

        public void Dispose()
        {
            if (_mcastSocket != null) {
                _mcastSocket.Close();
                _mcastSocket = null;
            }
        }
    }
}


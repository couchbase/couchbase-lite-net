//
//  P2PBroadcastMessage.cs
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
using System.Collections.Generic;
using System.IO;

namespace Couchbase.Lite.PeerToPeer.Messages
{
    public abstract class P2PMessage //: IDictionary<string, object>
    {
        private const string TAG = "P2PMessage";
        private static Dictionary<string, Func<P2PMessage>> _FactoryMap = new Dictionary<string, Type>()
        {
            { "heartbeat", () => new P2PHeartbeatMessage() }
        };

        private Dictionary<string, object> _parameters = new Dictionary<string, object>();
        private string _typeKey;

        protected abstract int MaxVersion { get; }
        public int Version { get; private set; }

        public static P2PMessage Deserialize(byte[] data)
        {
            BinaryReader br = new BinaryReader(new MemoryStream(data));
            string type = br.ReadString();
            P2PMessage retVal = null;
            if (!_FactoryMap.TryGetValue(type, out retVal)) {
                Log.E(TAG, "Unknown type key {0}", type);
                return null;
            }
                
            int version = br.ReadInt32();
            if (!retVal.VersionOk(version)) {
                Log.E(TAG, "Unknown message version {0} for type {1}", version, type);
                return null;
            }

            retVal._typeKey = type;
            retVal.Deserialize(br);
            return retVal;
        }

        public byte[] Serialize()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(stream);
            Serialize(bw);
            return stream.GetBuffer();
        }

        public abstract void Execute();

        protected virtual void Serialize(BinaryWriter bw) 
        {
            bw.Write(_typeKey);
            bw.Write(Version);
        }

        protected virtual void Deserialize(BinaryReader br)
        {
        }

        private bool VersionOk(int version)
        {
            return version <= MaxVersion;
        }
    }
}


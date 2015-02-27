//
//  P2PHeartbeatMessage_v1.cs
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
using System.IO;

namespace Couchbase.Lite.PeerToPeer.Messages
{
    internal sealed partial class P2PHeartbeatMessage : P2PMessage
    {
        private class v1 : P2PMessageSerializer<P2PHeartbeatMessage>
        {

            #region P2PMessageSerializer

            public override void Serialize(P2PHeartbeatMessage obj, BinaryWriter bw)
            {
                byte[] bytes = obj.User.ID.ToByteArray();
                bw.Write((byte)bytes.Length);
                Guid userId = bw.Write(obj.User.ID.ToByteArray());
            }

            public override void Deserialize(P2PHeartbeatMessage obj, BinaryReader br)
            {
                byte lengthByte = br.ReadByte();
                byte[] bytes = br.ReadBytes(lengthByte);
                obj.User = new P2PUser(new Guid(bytes));
            }

            #endregion

        }


    }
}


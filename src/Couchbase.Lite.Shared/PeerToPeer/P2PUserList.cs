//
//  P2PUserList.cs
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
using System.Linq;

namespace Couchbase.Lite.PeerToPeer
{
    public sealed class P2PUserList 
    {
        private List<P2PUser> _connectedUsers = new List<P2PUser>();

        public P2PUserList()
        {
            P2PNetwork.Broadcasting.UserStatusChanged += HandleUserStatusChanged;
        }

        void HandleUserStatusChanged (IP2PBroadcasting sender, UserStatusChangedEventArgs args)
        {
            var existingUser = _connectedUsers.FirstOrDefault(x => x.ID == args.UserId);
            if (existingUser == null && args.UserEvent == UserEvent.Connected) {
                P2PUser user = new P2PUser();
                _connectedUsers.Add(user);
            }
        }
    }


}


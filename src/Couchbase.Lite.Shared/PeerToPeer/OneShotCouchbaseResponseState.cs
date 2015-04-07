//
//  OneShotCouchbaseResponseState.cs
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
using Couchbase.Lite.Internal;
using System.Collections.Generic;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.PeerToPeer
{
    internal sealed class OneShotCouchbaseResponseState : ICouchbaseResponseState
    {
        private readonly Replication _replication;

        public CouchbaseLiteResponse Response { get; set; }

        public bool IsAsync 
        {
            get {
                return true;
            }
        }

        public OneShotCouchbaseResponseState(Replication replication)
        {
            if (replication == null) {
                throw new ArgumentNullException("replication");
            }

            _replication = replication;
            _replication.Changed += ReplicationChanged;
        }

        private void ReplicationChanged (object sender, ReplicationChangeEventArgs e)
        {
            var replication = (Replication)sender;
            replication.Changed -= ReplicationChanged;
            if (replication.Status == ReplicationStatus.Stopped) {
                Response.InternalStatus = replication.LastError == null ? StatusCode.Ok : StatusCode.InternalServerError;
                Response.WriteHeaders();
                Response.JsonBody = new Body(new NonNullDictionary<string, object> {
                    { "ok", replication.LastError != null ? (bool?)true : null },
                    { "session_id", replication.sessionID }
                });
                Response.WriteToContext();
            }
        }
    }
}


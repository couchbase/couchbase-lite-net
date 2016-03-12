//
// BadWebSocketChangeTracker.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using Couchbase.Lite.Internal;
using System.Threading.Tasks;

namespace Couchbase.Lite.Tests
{
    // Make sure that the correct behavior is exhibited when a non websocket response is received
    internal class BadWebSocketChangeTracker : WebSocketChangeTracker
    {
        public override Uri ChangesFeedUrl
        {
            get {
                var dbURLString = DatabaseUrl.ToString().Replace("http", "ws");
                if (!dbURLString.EndsWith("/", StringComparison.Ordinal)) {
                    dbURLString += "/";
                }

                dbURLString += "_changes?feed=continuous";
                return new Uri(dbURLString);
            }
        }

        public BadWebSocketChangeTracker(Uri databaseURL, bool includeConflicts, object lastSequenceID, 
            IChangeTrackerClient client, TaskFactory workExecutor = null)
            : base(databaseURL, includeConflicts, lastSequenceID, client, 2, workExecutor)
        {

        }
    }
}


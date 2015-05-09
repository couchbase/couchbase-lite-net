//
//  ChangesFeedMode.cs
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

namespace Couchbase.Lite.Replicator
{

    /// <summary>
    /// The mode to send or request a changes feed in
    /// </summary>
    [Serializable]
    public enum ChangesFeedMode
    {
        /// <summary>
        /// A one-shot
        /// </summary>
        Normal,

        /// <summary>
        /// The connection will remain open until the next change made to the database, and then the information
        /// will be sent and the connection closed
        /// </summary>
        LongPoll,

        /// <summary>
        /// Similar to LongPoll, but the connection remains open indefinitely
        /// </summary>
        Continuous,

        /// <summary>
        /// Uses the Mozilla event source format to sent changes continuously
        /// </summary>
        EventSource
    }
}


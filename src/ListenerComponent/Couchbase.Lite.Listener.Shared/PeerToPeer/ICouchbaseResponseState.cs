//
//  ICouchbaseResponseState.cs
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

namespace Couchbase.Lite.Listener
{
    
    /// <summary>
    /// An interface that stores information about an operation that
    /// will result in a P2P response
    /// </summary>
    public interface ICouchbaseResponseState
    {

        /// <summary>
        /// The response object that has the information needed to write
        /// the HTTP response
        /// </summary>
        CouchbaseLiteResponse Response { get; }

        /// <summary>
        /// Whether or not this operation is async
        /// </summary>
        /// <value><c>true</c> if this op is async; otherwise, <c>false</c>.</value>
        bool IsAsync { get; }

    }
}


//
//  AsyncOpCouchbaseResponseState.cs
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
    /// A class that stores the state of an HTTP response while an async operation
    /// is running so it can send a response to the client once the operation has 
    /// finished
    /// </summary>
    internal sealed class AsyncOpCouchbaseResponseState : ICouchbaseResponseState
    {

        #region Properties

        //ICouchbaseResponseState
        public CouchbaseLiteResponse Response { get; set; }

        //ICouchbaseResponseState
        public bool IsAsync 
        {
            get {
                return true;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Signal that the async operation has finished so that the response
        /// can be written
        /// </summary>
        public void SignalFinished()
        {
            Response.WriteHeaders();
            Response.WriteToContext();
            CouchbaseLiteRouter.ResponseFinished(this);
        }

        #endregion

    }
}


//
//  ReplicationCouchbaseResponseState.cs
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
using System.Collections.Generic;

using Couchbase.Lite.Replicator;

namespace Couchbase.Lite.Listener
{

    /// <summary>
    /// A class that will monitor replications and send data as they
    /// change
    /// </summary>
    internal sealed class ReplicationCouchbaseResponseState : ICouchbaseResponseState
    {

        #region Properties

        /// <summary>
        /// Gets or sets the manager used to open DBs, etc
        /// </summary>
        public Manager DbManager { get; set; }

        /// <summary>
        /// Gets or sets the changes feed mode used for listening to
        /// the replications
        /// </summary>
        /// <value>The changes feed mode.</value>
        public ChangesFeedMode ChangesFeedMode { get; set; }

        // ICouchbaseResponseState
        public CouchbaseLiteResponse Response { get; set; }

        // ICouchbaseResponseState
        public bool IsAsync {
            get {
                return true;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="replications">The list of replications to listen to</param>
        public ReplicationCouchbaseResponseState(IList<Replication> replications)
        {
            foreach (var repl in replications) {
                repl.Changed += ReplicationChanged;
            }
        }

        #endregion

        #region Private Methods

        // This method is called when an observed replication changes
        private void ReplicationChanged(object sender, ReplicationChangeEventArgs e)
        {
            var replication = (Replication)sender;
            if(replication.LocalDatabase.Manager == DbManager) {
                if(!Response.SendContinuousLine(replication.ActiveTaskInfo, ChangesFeedMode)) {
                    replication.Changed -= ReplicationChanged;
                    CouchbaseLiteRouter.ResponseFinished(this);
                }
            }
        }

        #endregion

    }
}


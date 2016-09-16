//
//  ReplicationStateTransition.cs
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
using Stateless;

namespace Couchbase.Lite.Replicator
{
    /// <summary>
    /// Represents a state transition that happens within the replicator
    /// </summary>
    public sealed class ReplicationStateTransition
    {

        #region Properties

        /// <summary>
        /// The state that the replication was in before the trigger
        /// </summary>
        public ReplicationState Source { get; internal set; }

        /// <summary>
        /// The state the replication was in after the trigger
        /// </summary>
        public ReplicationState Destination { get; internal set; }

        /// <summary>
        /// The trigger that caused the state change
        /// </summary>
        public ReplicationTrigger Trigger { get; internal set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="transition">The transition object that was created for the state change</param>
        public ReplicationStateTransition(StateMachine<ReplicationState, ReplicationTrigger>.Transition transition)
        {
            if (transition != null) {
                Source = transition.Source;
                Destination = transition.Destination;
                Trigger = transition.Trigger;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="source">The state that the replication was in before the trigger.</param>
        /// <param name="destination">The state the replication was in after the trigger.</param>
        /// <param name="trigger">The trigger that caused the state change.</param>
        public ReplicationStateTransition(ReplicationState source, ReplicationState destination, ReplicationTrigger trigger)
        {
            Source = source;
            Destination = destination;
            Trigger = trigger;
        }

        #endregion

    }
}


//
//  DatabaseChangedEventArgs.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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

namespace Couchbase.Lite
{
    /// <summary>
    /// The parameters of a database changed event
    /// </summary>
    public sealed class DatabaseChangedEventArgs : EventArgs
    {
        #region Properties

        /// <summary>
        /// Gets the document IDs of the changes that occurred
        /// </summary>
        public IList<string> DocIDs { get; }

        /// <summary>
        /// Gets whether or not this event was triggered by another 
        /// <see cref="IDatabase"/> instnace.
        /// </summary>
        public bool External { get; }

        /// <summary>
        /// Gets the last sequence checked before this event fired
        /// </summary>
        public ulong LastSequence { get; }

        #endregion

        #region Constructors

        internal DatabaseChangedEventArgs(IList<string> docIDs, ulong lastSequence, bool external)
        {
            DocIDs = docIDs;
            LastSequence = lastSequence;
            External = external;
        }

        #endregion
    }
}

//
//  DatabaseChangedEventArgs.cs
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
using System.Diagnostics;
using System.Linq;

namespace Couchbase.Lite
{
    /// <summary>
    /// [DEPRECATED] The parameters of a database changed event
    /// </summary>
    [Obsolete("DatabaseChangedEventArgs is deprecated, please use CollectionChangedEventArgs")]
    public class DatabaseChangedEventArgs : EventArgs
    {
        #region Properties

        /// <summary>
        /// Gets the database in which the change occurred
        /// </summary>
        public Database Database { get; }

        /// <summary>
        /// Gets the document that was changed
        /// </summary>
        public IReadOnlyList<string> DocumentIDs { get; }

        #endregion

        #region Constructors

        internal DatabaseChangedEventArgs(Database database, IReadOnlyList<string> documentIDs)
        {
            Database = database;
            DocumentIDs = documentIDs;
        }

        #endregion
    }
}
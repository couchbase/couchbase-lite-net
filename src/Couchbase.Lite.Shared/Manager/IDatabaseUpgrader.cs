//
//  IDatabaseUpgrader.cs
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

namespace Couchbase.Lite.Db
{
    /// <summary>
    /// An interface describing a class that can upgrade a database between
    /// versions of Couchbase Lite
    /// </summary>
    public interface IDatabaseUpgrader
    {
        /// <summary>
        /// Gets the document count of the database
        /// </summary>
        int NumDocs { get; }

        /// <summary>
        /// Gets the revision count of the database
        /// </summary>
        int NumRevs { get; }

        /// <summary>
        /// Gets or sets whether or not the upgrader is allowed to remove
        /// a previous attachment directory (if it has changed)
        /// </summary>
        bool CanRemoveOldAttachmentsDir { get; set; }

        /// <summary>
        /// Executes the import of an old Couchbase Lite database into a newer
        /// version
        /// </summary>
        void Import();

        /// <summary>
        /// Backs out of a failed upgrade
        /// </summary>
        void Backout();
    }
}


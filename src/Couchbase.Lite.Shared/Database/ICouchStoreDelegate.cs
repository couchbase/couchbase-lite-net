//
//  ICouchStoreDelegate.cs
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
using System.Collections.Generic;

namespace Couchbase.Lite.Store
{
    /// <summary>
    /// Delegate of a ICouchStore instance. Database implements this.
    /// </summary>
    internal interface ICouchStoreDelegate
    {

        /// <summary>
        /// Called whenever the outermost transaction completes.
        /// </summary>
        /// <param name="committed"><c>true</c> on commit, <c>false</c> if the transaction was aborted.</param>
        void StorageExitedTransaction(bool committed);

        /// <summary>
        /// Called whenever a revision is added to the database (but not for local docs or for purges.) 
        /// </summary>
        void DatabaseStorageChanged(DocumentChange change);

        /// <summary>
        /// Generates a revision ID for a new revision.
        /// </summary>
        /// <param name="json">The canonical JSON of the revision (with metadata properties removed.)</param>
        /// <param name="deleted"><c>true</c> if this revision is a deletion</param>
        /// <param name="prevRevId">The parent's revision ID, or nil if this is a new document.</param>
        string GenerateRevID(IEnumerable<byte> json, bool deleted, string prevRevId);

    }
}



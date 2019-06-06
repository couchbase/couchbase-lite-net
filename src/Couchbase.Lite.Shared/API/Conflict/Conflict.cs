// 
//  Conflict.cs
// 
//  Copyright (c) 2019 Couchbase, Inc All rights reserved.
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

using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Couchbase.Lite
{
    public class Conflict
    {
        /// <summary>
        /// The conflict resolved document id.
        /// </summary>
        [NotNull]
        public string DocumentID { get; }

        /// <summary>
        /// The document in local database. If null, the document is deleted.
        /// </summary>
        [CanBeNull]
        public Document LocalDocument { get; }

        /// <summary>
        /// The document in remote database. If null, the document is deleted.
        /// </summary>
        [CanBeNull]
        public Document RemoteDocument { get; }

        internal Conflict(string docID, Document localDoc, Document remoteDoc)
        {
            Debug.Assert(localDoc != null || remoteDoc != null,
                "Local and remote document shouldn't be empty at same time, when resolving conflict.");

            DocumentID = docID;
            LocalDocument = localDoc;
            RemoteDocument = remoteDoc;
        }
    }
}

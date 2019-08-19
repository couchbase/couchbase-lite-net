// 
//  IConflictResolver.cs
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

using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite
{
    /// <summary>
    /// Conflict Resolver Interface
    /// </summary>
    public interface IConflictResolver
    {
        /// <summary>
        /// The callback conflict resolve method, if conflict occurs.
        /// When a null document is returned, the conflict will be resolved as document deletion. 
        /// If there is an exception thrown in the resolve method, the exception will be caught and handled:
        /// <list type="bullet">
        /// <item>
        /// <description>1. The conflict resolving will be skipped. The pending conflicted documents will be resolved when the replicator is restarted.</description>
        /// </item>
        /// <item>
        /// <description>2. The exception will be reported in the warning log.</description>
        /// </item>
        /// <item>
        /// <description>3. The exception will be reported in the DocumentReplicationChange event.</description>
        /// </item>
        /// </list>
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a document from a different database is returned.</exception>
        Document Resolve(Conflict conflict);
    }
}

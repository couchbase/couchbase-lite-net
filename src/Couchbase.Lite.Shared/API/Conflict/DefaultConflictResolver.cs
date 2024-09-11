// 
//  DefaultConflictResolver.cs
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

namespace Couchbase.Lite
{
    internal sealed class DefaultConflictResolver : IConflictResolver
    {
        /// <summary>
        /// The callback default conflict resolve method, if conflict occurs.
        /// </summary>
        public Document? Resolve(Conflict conflict)
        {
            if (conflict.RemoteDocument == null || conflict.LocalDocument == null)
                return null;

            return conflict.LocalDocument.Timestamp > conflict.RemoteDocument.Timestamp
                ? conflict.LocalDocument
                : conflict.RemoteDocument;
        }
    }
}

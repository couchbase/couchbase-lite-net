// 
//  IModeledDocument.cs
// 
//  Author:
//  Jim Borden  <jim.borden@couchbase.com>
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

namespace Couchbase.Lite
{
    public interface IModeledDocument<T> : IThreadSafe, IDisposable where T : class, new()
    {
        #region Properties

        [AccessibilityMode(AccessMode.FromAnywhere)]
        IDatabase Db { get; }

        [AccessibilityMode(AccessMode.FromAnywhere)]
        string Id { get; }

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        bool IsDeleted { get; }

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        T Item { get; set; }

        [AccessibilityMode(AccessMode.FromAnywhere)]
        ulong Sequence { get; }

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        string Type { get; set; }

        #endregion

        #region Public Methods

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void Delete();

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void Save();

        #endregion
    }
}

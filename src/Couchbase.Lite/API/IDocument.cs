//
//  IDocument.cs
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

namespace Couchbase.Lite
{
    public interface IDocument : IPropertyContainer, IDisposable
    {
        event EventHandler<DocumentSavedEventArgs> Saved;

        [AccessibilityMode(AccessMode.FromAnywhere)]
        IDatabase Database { get; }

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        IConflictResolver ConflictResolver { get; set; }

        [AccessibilityMode(AccessMode.FromAnywhere)]
        string Id { get; }

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        bool IsDeleted { get; }

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        bool Exists { get; }

        [AccessibilityMode(AccessMode.FromAnywhere)]
        ulong Sequence { get; }

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        new IDocument Set(string key, object value);

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void Save();

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void Delete();

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        bool Purge();

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void Revert();
    }
}

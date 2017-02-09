//
//  IDatabase.cs
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
    public interface IDatabase : IThreadSafe, IDisposable
    {
        event EventHandler<DatabaseChangedEventArgs> Changed;

        [AccessibilityMode(AccessMode.FromAnywhere)]
        string Name { get; }

        [AccessibilityMode(AccessMode.FromAnywhere)]
        string Path { get; }

        [AccessibilityMode(AccessMode.FromAnywhere)]
        DatabaseOptions Options { get; }

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        IConflictResolver ConflictResolver { get; set; }

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void Close();

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void Delete();

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        bool InBatch(Func<bool> a);

        [AccessibilityMode(AccessMode.FromAnywhere)]
        IDocument CreateDocument();

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        IDocument GetDocument(string id);

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        bool DocumentExists(string documentID);

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        IDocument this[string id] { get; }

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void CreateIndex(string propertyPath);

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void CreateIndex(string propertyPath, IndexType indexType, IndexOptions options);

        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void DeleteIndex(string propertyPath, IndexType type);
    }
}

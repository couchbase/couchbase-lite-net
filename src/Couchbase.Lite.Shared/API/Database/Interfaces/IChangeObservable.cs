// 
//  IChangeObservable.cs
// 
//  Copyright (c) 2022 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Sync;
using JetBrains.Annotations;
using System;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public interface IChangeObservableRemovable
    {
        void RemoveChangeListener(ListenerToken token);
    }

    public interface IChangeObservable<TEventType> : IChangeObservableRemovable where TEventType : EventArgs
    {
        ListenerToken AddChangeListener([CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<TEventType> handler);

        ListenerToken AddChangeListener([NotNull] EventHandler<TEventType> handler);
    }

    public interface IDocumentChangeObservable : IChangeObservableRemovable
    {
        ListenerToken AddDocumentChangeListener([NotNull] string id, [CanBeNull] TaskScheduler scheduler,
            [NotNull] EventHandler<DocumentChangedEventArgs> handler);

        ListenerToken AddDocumentChangeListener([NotNull] string id, [NotNull] EventHandler<DocumentChangedEventArgs> handler);
    }

    public interface IDocumentReplicatedObservable : IChangeObservableRemovable
    {
        ListenerToken AddDocumentReplicationListener([NotNull] EventHandler<DocumentReplicationEventArgs> handler);

        ListenerToken AddDocumentReplicationListener([CanBeNull] TaskScheduler scheduler,
            [NotNull] EventHandler<DocumentReplicationEventArgs> handler);
    }
}

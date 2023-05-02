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
using System;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface describing an object which contains a change listener that can be removed
    /// </summary>
    public interface IChangeObservableRemovable
    {
        /// <summary>
        /// Remove a change listener by token
        /// </summary>
        /// <param name="token">The token returned from <see cref="IChangeObservable{TEventType}.AddChangeListener(EventHandler{TEventType})" /></param>
        void RemoveChangeListener(ListenerToken token);
    }

    /// <summary>
    /// An interface describing an object that can have a change listener added to it
    /// </summary>
    /// <typeparam name="TEventType">The type of arguments used by the change listener</typeparam>
    public interface IChangeObservable<TEventType> : IChangeObservableRemovable where TEventType : EventArgs
    {
        /// <summary>
        /// Adds a change listener that executes using the provided TaskScheduler
        /// </summary>
        /// <param name="scheduler">The scheduler to use (will use a default if null)</param>
        /// <param name="handler">The handler that the change listener should use to call back</param>
        /// <returns>A token to remove the change listener later</returns>
        ListenerToken AddChangeListener(TaskScheduler? scheduler, EventHandler<TEventType> handler);

        /// <summary>
        /// Adds a change listener using the default TaskScheduler
        /// </summary>
        /// <param name="handler">The handler that the change listener should use to call back</param>
        /// <returns>A token to remove the change listener later</returns>
        ListenerToken AddChangeListener(EventHandler<TEventType> handler);
    }

    /// <summary>
    /// An interface describing an object that can add a change listener at a <see cref="Document"/>
    /// level
    /// </summary>
    public interface IDocumentChangeObservable : IChangeObservableRemovable
    {
        /// <summary>
        /// Adds a change listener that executes using the provided TaskScheduler
        /// </summary>
        /// <param name="id">The ID of the document to monitor</param>
        /// <param name="scheduler">The scheduler to use (will use a default if null)</param>
        /// <param name="handler">The handler that the change listener should use to call back</param>
        /// <returns>A token to remove the change listener later</returns>
        ListenerToken AddDocumentChangeListener(string id, TaskScheduler? scheduler,
            EventHandler<DocumentChangedEventArgs> handler);

        /// <summary>
        /// Adds a change listener that executes using the default TaskScheduler
        /// </summary>
        /// <param name="id">The ID of the document to monitor</param>
        /// <param name="handler">The handler that the change listener should use to call back</param>
        /// <returns>A token to remove the change listener later</returns>
        ListenerToken AddDocumentChangeListener(string id, EventHandler<DocumentChangedEventArgs> handler);
    }

    /// <summary>
    /// An interface describing an object that can add a change listener for when
    /// a document is replicated
    /// </summary>
    public interface IDocumentReplicatedObservable : IChangeObservableRemovable
    {
        /// <summary>
        /// Adds a change listener using the default TaskScheduler
        /// </summary>
        /// <param name="handler">The handler that the change listener should use to call back</param>
        /// <returns>A token to remove the change listener later</returns>
        ListenerToken AddDocumentReplicationListener(EventHandler<DocumentReplicationEventArgs> handler);

        /// <summary>
        /// Adds a change listener that executes using the provided TaskScheduler
        /// </summary>
        /// <param name="scheduler">The scheduler to use (will use a default if null)</param>
        /// <param name="handler">The handler that the change listener should use to call back</param>
        /// <returns>A token to remove the change listener later</returns>
        ListenerToken AddDocumentReplicationListener(TaskScheduler? scheduler,
            EventHandler<DocumentReplicationEventArgs> handler);
    }
}

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
    /// <summary>
    /// An interface describing a Couchbase Lite document
    /// </summary>
    public interface IDocument : IPropertyContainer, IDisposable/*, IModellable*/
    {
        #region Properties

        /// <summary>
        /// Gets or sets the <see cref="IConflictResolver"/> that should resolve conflicts for this document
        /// </summary>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        IConflictResolver ConflictResolver { get; set; }

        /// <summary>
        /// Gets the <see cref="IDatabase"/> that owns this document
        /// </summary>
        [AccessibilityMode(AccessMode.FromAnywhere)]
        IDatabase Database { get; }

        /// <summary>
        /// Gets whether or not this document exists (i.e. has been persisted)
        /// </summary>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        bool Exists { get; }

        /// <summary>
        /// Gets the unique ID of this document
        /// </summary>
        [AccessibilityMode(AccessMode.FromAnywhere)]
        string Id { get; }

        /// <summary>
        /// Gets whether or not this document is deleted
        /// </summary>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        bool IsDeleted { get; }

        /// <summary>
        /// Gets the sequence number of this document
        /// </summary>
        [AccessibilityMode(AccessMode.FromAnywhere)]
        ulong Sequence { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Deletes the document
        /// </summary>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        void Delete();

        /// <summary>
        /// Purges the document, which leaves no trace behind for replication
        /// </summary>
        /// <returns>Whether or not the purge succeeded</returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        bool Purge();

        /// <summary>
        /// Saves the document to disk
        /// </summary>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        void Save();

        /// <summary>
        /// An event that is fired when the document is saved
        /// </summary>
        event EventHandler<DocumentSavedEventArgs> Saved;

        /// <summary>
        /// Sets the given key to the given value in this document
        /// </summary>
        /// <param name="key">The key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The same <see cref="IDocument"/> object for chaining</returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromOwningThreadOnly)]
        new IDocument Set(string key, object value);

        #endregion
    }
}

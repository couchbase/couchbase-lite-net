//
//  IPropertyContainer.cs
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
using System.Collections.Generic;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface describing an object that can hold arbitrary JSON properties
    /// </summary>
    public interface IPropertyContainer : IThreadSafe
    {
        #region Properties

        /// <summary>
        /// Bracket operator for returning an untyped property from this object
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns>The value for the key, or <c>null</c> if the key has no value</returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        object this[string key] { get; set; }

        /// <summary>
        /// Gets or sets the raw properties for the object
        /// </summary>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        IDictionary<string, object> Properties { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets whether or not this object contains a given key
        /// </summary>
        /// <param name="key">The key to check for</param>
        /// <returns>whether or not this object contains a given key</returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        bool Contains(string key);

        /// <summary>
        /// Gets the untyped value for the given key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value stored in the given key, or <c>null</c> if it doesn't exist</returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        object Get(string key);

        /// <summary>
        /// Gets the <see cref="IList{T}"/> of <see cref="Object"/> for the given key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value stored in the given key, or <c>null</c> if it doesn't exist
        /// or is not an <see cref="IList{T}"/> of <see cref="Object"/></returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        IList<object> GetArray(string key);

        /// <summary>
        /// Gets the <see cref="IBlob"/> for the given key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value stored in the given key, or <c>null</c> if it doesn't exist
        /// or is not a <see cref="IBlob"/></returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        IBlob GetBlob(string key);

        /// <summary>
        /// Gets the <see cref="Boolean"/> for the given key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value stored in the given key, or <c>false</c> if it doesn't exist
        /// or is not a <see cref="Boolean"/></returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        bool GetBoolean(string key);

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/>? for the given key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value stored in the given key, or <c>null</c> if it doesn't exist
        /// or is not a <see cref="DateTimeOffset"/></returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        DateTimeOffset? GetDate(string key);

        /// <summary>
        /// Gets the <see cref="Double"/> for the given key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value stored in the given key, or <c>0.0</c> if it doesn't exist
        /// or is not a <see cref="Double"/></returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        double GetDouble(string key);

        /// <summary>
        /// Gets the <see cref="Single"/> for the given key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value stored in the given key, or <c>0.0f</c> if it doesn't exist
        /// or is not a <see cref="Single"/></returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        float GetFloat(string key);

        /// <summary>
        /// Gets the <see cref="Int64"/> for the given key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value stored in the given key, or <c>0L</c> if it doesn't exist
        /// or is not a <see cref="Int64"/></returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        long GetLong(string key);

        /// <summary>
        /// Gets the <see cref="String"/> for the given key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value stored in the given key, or <c>null</c> if it doesn't exist
        /// or is not a <see cref="String"/></returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        string GetString(string key);

        /// <summary>
        /// Gets the <see cref="ISubdocument"/> for the given key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value stored in the given key, or <c>null</c> if it doesn't exist
        /// or is not an <see cref="ISubdocument"/></returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        ISubdocument GetSubdocument(string key);

        /// <summary>
        /// Removes the given key from the object
        /// </summary>
        /// <param name="key">The key to remove</param>
        /// <returns>Returns itself (so that this call can be chained)</returns>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        IPropertyContainer Remove(string key);

        /// <summary>
        /// Cancels all changes since the last save
        /// </summary>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        void Revert();

        /// <summary>
        /// Sets the given value to the given key in the object
        /// </summary>
        /// <param name="key">The key to set</param>
        /// <param name="value">The value to use</param>
        /// <returns>Returns itself (so that this call can be chained)</returns>
        /// <exception cref="ArgumentException"><c>value</c> is not a valid JSON type</exception>
        /// <exception cref="ThreadSafetyViolationException">Thrown if an invalid access attempt is made</exception>
        [AccessibilityMode(AccessMode.FromQueueOnly)]
        IPropertyContainer Set(string key, object value);

        #endregion
    }
}

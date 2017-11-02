// 
// IReadOnlyDictionary.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface representing a readonly key-value collection with type-safe accessors
    /// </summary>
    public interface IReadOnlyDictionary : IReadOnlyDictionaryFragment, IReadOnlyCollection<KeyValuePair<string, object>>
    {
        #region Properties

        /// <summary>
        /// Gets all the keys held by this dictionary
        /// </summary>
        ICollection<string> Keys { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if a given key is present in the dictionary
        /// </summary>
        /// <param name="key">The key to check for</param>
        /// <returns><c>true</c> if the dictionary contains the key, else <c>false</c></returns>
        bool Contains(string key);

        /// <summary>
        /// Gets the value of a given key as an <see cref="IReadOnlyArray"/>
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or <c>null</c></returns>
        IReadOnlyArray GetArray(string key);

        /// <summary>
        /// Gets the value of a given key as a <see cref="Blob"/>
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or <c>null</c></returns>
        Blob GetBlob(string key);

        /// <summary>
        /// Gets the value of a given key as a <see cref="Boolean"/>
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or its converted equivalent</returns>
        /// <remarks>Any non-zero object will be treated as true, so don't rely on 
        /// any sort of parsing</remarks>
        bool GetBoolean(string key);

        /// <summary>
        /// Gets the value of a given key as a <see cref="DateTimeOffset"/>
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or a default value</returns>
        DateTimeOffset GetDate(string key);

        /// <summary>
        /// Gets the value of a given key as a readonly dictionary
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or <c>null</c></returns>
        IReadOnlyDictionary GetDictionary(string key);

        /// <summary>
        /// Gets the value of a given key as a <see cref="Double"/>
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or its converted equivalent</returns>
        /// <remarks><c>true</c> will be converted to 1.0, and everything else that
        /// is non-numeric will be 0.0</remarks>
        double GetDouble(string key);

        /// <summary>
        /// Gets the value of a given key as a <see cref="Single"/>
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or its converted equivalent</returns>
        /// <remarks><c>true</c> will be converted to 1.0f, and everything else that
        /// is non-numeric will be 0.0f</remarks>
        float GetFloat(string key);

        /// <summary>
        /// Gets the value of a given key as an <see cref="Int32"/>
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or its converted equivalent</returns>
        /// <remarks><c>true</c> will be converted to 1, a <see cref="Double"/> value
        /// will be rounded, and everything else non-numeric will be 0</remarks>
        int GetInt(string key);

        /// <summary>
        /// Gets the value of a given key as an <see cref="Int64"/>
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or its converted equivalent</returns>
        /// <remarks><c>true</c> will be converted to 1, a <see cref="Double"/> value
        /// will be rounded, and everything else non-numeric will be 0</remarks>
        long GetLong(string key);

        /// <summary>
        /// Gets the value of a given key as an untyped object
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or <c>null</c></returns>
        /// <remarks>This method should be avoided for numeric types, whose
        /// underlying representation is subject to change and thus
        /// <see cref="InvalidCastException"/>s </remarks>
        object GetObject(string key);

        /// <summary>
        /// Gets the value of a given key as a <see cref="String"/>
        /// </summary>
        /// <param name="key">The key to check the value for</param>
        /// <returns>The contained value, or <c>null</c></returns>
        string GetString(string key);

        /// <summary>
        /// Converts this object to a standard .NET string to object
        /// <see cref="Dictionary{TKey, TValue}"/>
        /// </summary>
        /// <returns>The contents of this object as a .NET dictionary</returns>
        Dictionary<string, object> ToDictionary();

        #endregion
    }
}

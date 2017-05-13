// 
// IReadOnlyArray.cs
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
    /// An interface representing a read-only linear collection of objects
    /// </summary>
    public interface IReadOnlyArray : IReadOnlyArrayFragment, IEnumerable<object>
    {
        #region Properties

        /// <summary>
        /// Gets the number of elements in the collection
        /// </summary>
        int Count { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the value at the given index as a read only array
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        IReadOnlyArray GetArray(int index);

        /// <summary>
        /// Gets the value at the given index as a <see cref="Blob"/>
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        Blob GetBlob(int index);

        /// <summary>
        /// Gets the value at the given index as a <see cref="Boolean"/>
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or its converted equivalent</returns>
        /// <remarks>Any non-zero object will be treated as true, so don't rely on 
        /// any sort of parsing</remarks>
        bool GetBoolean(int index);

        /// <summary>
        /// Gets the value at the given index as a <see cref="DateTimeOffset"/>
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or a default</returns>
        DateTimeOffset GetDate(int index);

        /// <summary>
        /// Gets the value at the given index as an <see cref="IReadOnlyDictionary"/>
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        IReadOnlyDictionary GetDictionary(int index);

        /// <summary>
        /// Gets the value at the given index as a <see cref="Double"/>
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or its converted equivalent</returns>
        /// <remarks><c>true</c> will be converted to 1.0, and everything else that
        /// is non-numeric will be 0.0</remarks>
        double GetDouble(int index);

        /// <summary>
        /// Gets the value at the given index as an <see cref="Int32"/>
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or its converted equivalent</returns>
        /// <remarks><c>true</c> will be converted to 1, a <see cref="Double"/> value
        /// will be rounded, and everything else non-numeric will be 0</remarks>
        int GetInt(int index);

        /// <summary>
        /// Gets the value at the given index as an <see cref="Int64"/>
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or its converted equivalent</returns>
        /// <remarks><c>true</c> will be converted to 1, a <see cref="Double"/> value
        /// will be rounded, and everything else non-numeric will be 0</remarks>
        long GetLong(int index);

        /// <summary>
        /// Gets the value at the given index as an untyped object
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        /// <remarks>This method should be avoided for numeric types, whose
        /// underlying representation is subject to change and thus
        /// <see cref="InvalidCastException"/>s </remarks>
        object GetObject(int index);

        /// <summary>
        /// Gets the value at the given index as a <see cref="String"/>
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        string GetString(int index);

        /// <summary>
        /// Converts this object to a standard .NET collection
        /// </summary>
        /// <returns>The contents of this collection as a .NET collection</returns>
        IList<object> ToList();

        #endregion
    }
}

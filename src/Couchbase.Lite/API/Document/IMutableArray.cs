// 
// ArrayObject.cs
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
using System.Collections;

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface representing a writeable collection of objects
    /// </summary>
    public interface IMutableArray : IArray, IMutableArrayFragment
    {
        #region Properties

        /// <summary>
        /// Gets the value of the given index, or lack thereof,
        /// wrapped inside of a <see cref="IMutableFragment"/>
        /// </summary>
        /// <param name="index">The index to check</param>
        /// <returns>The value of the given index, or lack thereof</returns>
        [NotNull]
        new IMutableFragment this[int index] { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddValue(object value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddString(string value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddInt(int value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddLong(long value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddFloat(float value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddDouble(double value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddBoolean(bool value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddBlob(Blob value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddDate(DateTimeOffset value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddArray(ArrayObject value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray AddDictionary(DictionaryObject value);

        /// <summary>
        /// Gets the value at the given index as an array
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        [CanBeNull]
        new MutableArray GetArray(int index);

        /// <summary>
        /// Gets the value at the given index as a dictionary
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        [CanBeNull]
        new MutableDictionary GetDictionary(int index);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertValue(int index, object value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertString(int index, string value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertInt(int index, int value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertLong(int index, long value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertFloat(int index, float value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertDouble(int index, double value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertBoolean(int index, bool value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertBlob(int index, Blob value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertDate(int index, DateTimeOffset value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertArray(int index, ArrayObject value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray InsertDictionary(int index, DictionaryObject value);

        /// <summary>
        /// Removes the item at the given index
        /// </summary>
        /// <param name="index">The index at which to remove the item</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray RemoveAt(int index);

        /// <summary>
        /// Replaces the contents of this collection with the contents of the given one
        /// </summary>
        /// <param name="array">The contents to replace the current contents</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray Set(IList array);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetValue(int index, object value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetString(int index, string value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetInt(int index, int value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetLong(int index, long value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetFloat(int index, float value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetDouble(int index, double value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetBoolean(int index, bool value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetBlob(int index, Blob value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetDate(int index, DateTimeOffset value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetArray(int index, ArrayObject value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        [NotNull]
        IMutableArray SetDictionary(int index, DictionaryObject value);



        #endregion
    }
}

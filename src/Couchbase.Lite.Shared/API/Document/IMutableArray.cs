﻿// 
// IMutableArray.cs
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

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface representing a writable collection of objects
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
        new IMutableFragment this[int index] { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddValue(object? value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddString(string? value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddInt(int value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddLong(long value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddFloat(float value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddDouble(double value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddBoolean(bool value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddBlob(Blob? value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddDate(DateTimeOffset value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddArray(ArrayObject? value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray AddDictionary(DictionaryObject? value);

        /// <summary>
        /// Gets the value at the given index as an array
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        new MutableArrayObject? GetArray(int index);

        /// <summary>
        /// Gets the value at the given index as a dictionary
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        new MutableDictionaryObject? GetDictionary(int index);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertValue(int index, object? value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertString(int index, string? value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertInt(int index, int value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertLong(int index, long value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertFloat(int index, float value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertDouble(int index, double value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertBoolean(int index, bool value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertBlob(int index, Blob? value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertDate(int index, DateTimeOffset value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertArray(int index, ArrayObject? value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray InsertDictionary(int index, DictionaryObject? value);

        /// <summary>
        /// Removes the item at the given index
        /// </summary>
        /// <param name="index">The index at which to remove the item</param>
        /// <returns>The array for further processing</returns>
        IMutableArray RemoveAt(int index);

        /// <summary>
        /// Replaces the contents of this collection with the contents of the given one
        /// </summary>
        /// <param name="array">The contents to replace the current contents</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetData(IList array);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetValue(int index, object? value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetString(int index, string? value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetInt(int index, int value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetLong(int index, long value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetFloat(int index, float value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetDouble(int index, double value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetBoolean(int index, bool value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetBlob(int index, Blob? value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetDate(int index, DateTimeOffset value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetArray(int index, ArrayObject? value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetDictionary(int index, DictionaryObject? value);

        /// <summary>
        /// Replaces the contents of this collection with the contents of the 
        /// given json string
        /// </summary>
        /// <remarks>
        /// json string must be constructed from <see cref="IJSON.ToJSON">ToJSON</see>
        /// </remarks>
        /// <param name="json">The json string to replace the current contents with</param>
        /// <returns>The array for further processing</returns>
        IMutableArray SetJSON(string json);

        #endregion
    }
}

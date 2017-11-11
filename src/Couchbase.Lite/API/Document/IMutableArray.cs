// 
// IArray.cs
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
        /// wrapped inside of a <see cref="Fragment"/>
        /// </summary>
        /// <param name="index">The index to check</param>
        /// <returns>The value of the given index, or lack thereof</returns>
        new Fragment this[int index] { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(object value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(string value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(int value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(long value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(float value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(double value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(bool value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(Blob value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(DateTimeOffset value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(MutableArray value);

        /// <summary>
        /// Adds an entry to this collection
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Add(DictionaryObject value);

        /// <summary>
        /// Gets the value at the given index as an array
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        new IMutableArray GetArray(int index);

        /// <summary>
        /// Gets the value at the given index as an <see cref="IDictionaryObject"/>
        /// </summary>
        /// <param name="index">The index to lookup</param>
        /// <returns>The value at the index, or <c>null</c></returns>
        new IMutableDictionary GetDictionary(int index);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, object value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, string value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, int value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, long value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, float value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, double value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, bool value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, Blob value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, DateTimeOffset value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, MutableArray value);

        /// <summary>
        /// Inserts a given value at the given index
        /// </summary>
        /// <param name="index">The index to insert the item at</param>
        /// <param name="value">The item to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Insert(int index, DictionaryObject value);

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
        IMutableArray Set(IList array);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, object value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, string value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, int value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, long value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, float value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, double value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, bool value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, Blob value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, DateTimeOffset value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, MutableArray value);

        /// <summary>
        /// Overwrites the value at the given index with the given value
        /// </summary>
        /// <param name="index">The index to overwrite</param>
        /// <param name="value">The value to insert</param>
        /// <returns>The array for further processing</returns>
        IMutableArray Set(int index, DictionaryObject value);



        #endregion
    }
}

//
// ContentValues.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Lite.Store
{

    /// <summary>
    /// A class for holding arbitrary values for binding to SQL statements and such
    /// </summary>
    public sealed class ContentValues // TODO: Create Add override and refactor to use initializer syntax.
    {

        #region Constants

        private const string Tag = "ContentValues";

        #endregion

        #region Variables

        //The actual container for storing the values
        private readonly Dictionary<string, object> mValues;

        #endregion

        #region Properties

        internal object this[string key] {
            get { return mValues[key]; }
            set { mValues[key] = value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an empty set of values using the default initial size
        /// </summary>
        public ContentValues()
        {
            // COPY: Copied from android.content.ContentValues
            // Choosing a default size of 8 based on analysis of typical
            // consumption by applications.
            mValues = new Dictionary<string, object>(8);
        }

        /// <summary>
        /// Creates an empty set of values using the given initial size
        /// </summary>
        /// <param name="size">the initial size of the set of values</param>
        public ContentValues(int size)
        {
            mValues = new Dictionary<String, Object>(size);
        }

        /// <summary>
        /// Creates a set of values copied from the given set
        /// </summary>
        /// <param name="from">The values to copy</param>
        public ContentValues(ContentValues from)
        {
            mValues = new Dictionary<string, object>(from.mValues);
        }

        #endregion

        #region Public Methods
        
        /// <summary>Adds a value to the set.</summary>
        /// <param name="key">the name of the value to put</param>
        /// <param name="value">the data for the value to put</param>
        public void Put(string key, string value)
        {
            mValues[key] = value;
        }

        /// <summary>Adds all values from the passed in ContentValues.</summary>
        /// <param name="other">the ContentValues from which to copy</param>
        public void PutAll(ContentValues other)
        {
            mValues.PutAll(other.mValues);
        }

        /// <summary>Adds a value to the set.</summary>
        /// <param name="key">the name of the value to put</param>
        /// <param name="value">the data for the value to put</param>
        public void Put(string key, byte value)
        {
            mValues[key] = value;
        }

        /// <summary>Adds a value to the set.</summary>
        /// <param name="key">the name of the value to put</param>
        /// <param name="value">the data for the value to put</param>
        public void Put(string key, short value)
        {
            mValues[key] = value;
        }

        /// <summary>Adds a value to the set.</summary>
        /// <param name="key">the name of the value to put</param>
        /// <param name="value">the data for the value to put</param>
        public void Put(string key, int value)
        {
            mValues[key] = value;
        }

        /// <summary>Adds a value to the set.</summary>
        /// <param name="key">the name of the value to put</param>
        /// <param name="value">the data for the value to put</param>
        public void Put(string key, long value)
        {
            mValues[key] = value;
        }

        /// <summary>Adds a value to the set.</summary>
        /// <param name="key">the name of the value to put</param>
        /// <param name="value">the data for the value to put</param>
        public void Put(string key, float value)
        {
            mValues[key] = value;
        }

        /// <summary>Adds a value to the set.</summary>
        /// <param name="key">the name of the value to put</param>
        /// <param name="value">the data for the value to put</param>
        public void Put(string key, double value)
        {
            mValues[key] = value;
        }

        /// <summary>Adds a value to the set.</summary>
        /// <param name="key">the name of the value to put</param>
        /// <param name="value">the data for the value to put</param>
        public void Put(string key, bool value)
        {
            mValues[key] = value;
        }

        /// <summary>Adds a value to the set.</summary>
        /// <param name="key">the name of the value to put</param>
        /// <param name="value">the data for the value to put</param>
        public void Put(string key, IEnumerable<Byte> value)
        {
            mValues[key] = value;
        }

        /// <summary>Adds a null value to the set.</summary>
        /// <param name="key">the name of the value to make null</param>
        public void PutNull(string key)
        {
            mValues[key] = null;
        }

        /// <summary>Returns the number of values.</summary>
        /// <returns>the number of values</returns>
        public int Size()
        {
            return mValues.Count;
        }

        /// <summary>Remove a single value.</summary>
        /// <param name="key">the name of the value to remove</param>
        public void Remove(string key)
        {
            mValues.Remove(key);
        }

        /// <summary>Removes all values.</summary>
        public void Clear()
        {
            mValues.Clear();
        }

        /// <summary>Returns true if this object has the named value.</summary>
        /// <param name="key">the value to check for</param>
        /// <returns>
        /// 
        /// <code>true</code>
        /// if the value is present,
        /// <code>false</code>
        /// otherwise
        /// </returns>
        public bool ContainsKey(string key)
        {
            return mValues.ContainsKey(key);
        }

        /// <summary>
        /// Returns the value of the specified key, or null if not present
        /// </summary>
        /// <param name="key">The key to check</param>
        public object Get(string key)
        {
            return mValues.Get(key);
        }

        /// <summary>
        /// Gets a value and converts it to a String.
        /// </summary>
        /// <param name="key">the value to get</param>
        /// <returns>the String for the value</returns>
        public string GetAsString(string key)
        {
            object value = mValues.Get(key);
            return value != null ? value.ToString() : null;
        }

        /// <summary>
        /// Gets a value and converts it to a Long.
        /// </summary>
        /// <param name="key">the value to get</param>
        /// <returns>the Long value, or null if the value is missing or cannot be converted</returns>
        public long? GetAsLong(string key)
        {
            return mValues.GetNullable<long>(key);
        }

        /// <summary>
        /// Gets a value and converts it to an Integer.
        /// </summary>
        /// <param name="key">the value to get</param>
        /// <returns>the Integer value, or null if the value is missing or cannot be converted</returns>
        public int? GetAsInteger(string key)
        {
            return mValues.GetNullable<int>(key);
        }

        /// <summary>
        /// Gets a value and converts it to a Short.
        /// </summary>
        /// <param name="key">the value to get</param>
        /// <returns>the Short value, or null if the value is missing or cannot be converted</returns>
        public short? GetAsShort(string key)
        {
            return mValues.GetNullable<short>(key);
        }

        /// <summary>
        /// Gets a value and converts it to a Byte.
        /// </summary>
        /// <param name="key">the value to get</param>
        /// <returns>the Byte value, or null if the value is missing or cannot be converted</returns>
        public byte? GetAsByte(string key)
        {
            return mValues.GetNullable<byte>(key);
        }

        /// <summary>
        /// Gets a value and converts it to a Double.
        /// </summary>
        /// <param name="key">the value to get</param>
        /// <returns>the Double value, or null if the value is missing or cannot be converted</returns>
        public double? GetAsDouble(string key)
        {
            return mValues.GetNullable<double>(key);
        }

        /// <summary>
        /// Gets a value and converts it to a Float.
        /// </summary>
        /// <param name="key">the value to get</param>
        /// <returns>the Float value, or null if the value is missing or cannot be converted</returns>
        public float? GetAsFloat(string key)
        {
            return mValues.GetNullable<float>(key);
        }

        /// <summary>
        /// Gets a value and converts it to a Boolean.
        /// </summary>
        /// <param name="key">the value to get</param>
        /// <returns>the Boolean value, or null if the value is missing or cannot be converted</returns>
        public bool? GetAsBoolean(string key)
        {
            return mValues.GetNullable<bool>(key);
        }

        /// <summary>
        /// Gets a value that is a byte array.
        /// </summary>
        /// <remarks>
        /// Gets a value that is a byte array. Note that this method will not convert
        /// any other types to byte arrays.
        /// </remarks>
        /// <param name="key">the value to get</param>
        /// <returns>the byte[] value, or null is the value is missing or not a byte[]</returns>
        public byte[] GetAsByteArray(string key)
        {
            return mValues.GetCast<byte[]>(key);
        }

        /// <summary>
        /// Returns a set of all of the keys and values
        /// </summary>
        /// <returns>a set of all of the keys and values</returns>
        public ICollection<KeyValuePair<string, object>> ValueSet()
        {
            return mValues.AsSafeEnumerable().ToArray();
        }

        /// <summary>Returns a set of all of the keys</summary>
        /// <returns>a set of all of the keys</returns>
        public ICollection<string> KeySet()
        {
            return mValues.Keys;
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="Couchbase.Lite.Store.ContentValues"/>.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="Couchbase.Lite.Store.ContentValues"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current
        /// <see cref="Couchbase.Lite.Store.ContentValues"/>; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (!(obj is ContentValues))
            {
                return false;
            }
            return mValues.Equals(((ContentValues)obj).mValues);
        }

        /// <summary>
        /// Serves as a hash function for a <see cref="Couchbase.Lite.Store.ContentValues"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a
        /// hash table.</returns>
        public override int GetHashCode()
        {
            return mValues.GetHashCode();
        }

        /// <summary>
        /// Returns a string containing a concise, human-readable description of this object.
        /// </summary>
        /// <returns>a printable representation of this object.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string name in mValues.Keys)
            {
                string value = GetAsString(name);
                if (sb.Length > 0)
                {
                    sb.Append(" ");
                }
                sb.Append(name + "=" + value);
            }
            return sb.ToString();
        }

        #endregion

    }
}

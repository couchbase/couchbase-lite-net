// 
// ReadOnlyFragment.cs
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
using Couchbase.Lite.Internal.Doc;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing an arbitrary readonly entry in a key path
    /// (e.g. object["key"][index]["next_key"], etc)
    /// </summary>
    public class ReadOnlyFragment : IReadOnlyArrayFragment, IReadOnlyDictionaryFragment
    {
        #region Properties

        /// <summary>
        /// Gets whether or not this object exists in the hierarchy
        /// </summary>
        public virtual bool Exists => Value != null;

        /// <inheritdoc />
        public ReadOnlyFragment this[int index]
        {
            get {
                if (Value is IReadOnlyArray a) {
                    return a[index];
                }

                return new ReadOnlyFragment(null);
            }
        }

        /// <inheritdoc />
        public ReadOnlyFragment this[string key]
        {
            get {
                if (Value is IReadOnlyDictionary d) {
                    return d[key];
                }

                return new ReadOnlyFragment(null);
            }
        }

        /// <summary>
        /// Gets the raw contained value of this object
        /// </summary>
        public object Value { get; }

        #endregion

        #region Constructors

        internal ReadOnlyFragment(object value)
        {
            Value = value;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the contained value as a <see cref="ReadOnlyArray"/>
        /// </summary>
        /// <returns>The cast contained value, or <c>null</c></returns>
        public ReadOnlyArray ToArray()
        {
            return Value as ReadOnlyArray;
        }

        /// <summary>
        /// Gets the contained value as a <see cref="Blob"/>
        /// </summary>
        /// <returns>The cast contained value, or <c>null</c></returns>
        public Blob ToBlob()
        {
            return Value as Blob;
        }

        /// <summary>
        /// Gets the contained value as a <see cref="Boolean"/>
        /// </summary>
        /// <returns>The cast contained value</returns>
        /// <remarks>Any non-zero object will be treated as true, so don't rely on 
        /// any sort of parsing</remarks>
        public bool ToBoolean()
        {
            return DataOps.ConvertToBoolean(Value);
        }

        /// <summary>
        /// Gets the contained value as a <see cref="DateTimeOffset"/>
        /// </summary>
        /// <returns>The cast contained value, or a default value</returns>
        public DateTimeOffset ToDate()
        {
            return DataOps.ConvertToDate(Value);
        }

        /// <summary>
        /// Gets the contained value as a <see cref="ReadOnlyDictionary"/>
        /// </summary>
        /// <returns>The cast contained value, or <c>null</c></returns>
        public ReadOnlyDictionary ToDictionary()
        {
            return Value as ReadOnlyDictionary;
        }

        /// <summary>
        /// Gets the contained value as a <see cref="Double"/>
        /// </summary>
        /// <returns>The cast contained value</returns>
        /// <remarks><c>true</c> will be converted to 1.0, and everything else that
        /// is non-numeric will be 0.0</remarks>
        public double ToDouble()
        {
            return DataOps.ConvertToDouble(Value);
        }

        /// <summary>
        /// Gets the contained value as a <see cref="Single"/>
        /// </summary>
        /// <returns>The cast contained value</returns>
        /// <remarks><c>true</c> will be converted to 1.0f, and everything else that
        /// is non-numeric will be 0.0f</remarks>
        public float ToFloat()
        {
            return DataOps.ConvertToFloat(Value);
        }

        /// <summary>
        /// Gets the contained value as an <see cref="Int32"/>
        /// </summary>
        /// <returns>The cast contained value</returns>
        /// <remarks><c>true</c> will be converted to 1, a <see cref="Double"/> value
        /// will be rounded, and everything else non-numeric will be 0</remarks>
        public int ToInt()
        {
            return DataOps.ConvertToInt(Value);
        }

        /// <summary>
        /// Gets the contained value as an <see cref="Int64"/>
        /// </summary>
        /// <returns>The cast contained value</returns>
        /// <remarks><c>true</c> will be converted to 1, a <see cref="Double"/> value
        /// will be rounded, and everything else non-numeric will be 0</remarks>
        public long ToLong()
        {
            return DataOps.ConvertToLong(Value);
        }

        /// <summary>
        /// Gets the contained value as an untyped object
        /// </summary>
        /// <returns>The contained value, or <c>null</c></returns>
        ///  <remarks>This method should be avoided for numeric types, whose
        /// underlying representation is subject to change and thus
        /// <see cref="InvalidCastException"/>s </remarks>
        public object ToObject()
        {
            return Value;
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return Value as string;
        }

        #endregion
    }
}

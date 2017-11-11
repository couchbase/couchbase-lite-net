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
using System.Diagnostics;
using Couchbase.Lite.Internal.Doc;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing an arbitrary readonly entry in a key path
    /// (e.g. object["key"][index]["next_key"], etc)
    /// </summary>
    public class Fragment : IArrayFragment, IDictionaryFragment
    {
        #region Constants

        public static readonly Fragment Null = new Fragment(null, null);

        #endregion

        #region Variables

        protected int _index;
        protected string _key;
        protected object _parent;

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether or not this object exists in the hierarchy
        /// </summary>
        public bool Exists => ToObject() != null;

        /// <summary>
        /// Gets the value of the fragment as an untyped object (set will throw)
        /// </summary>
        public virtual object Value
        {
            get => ToObject();
            set => throw new InvalidOperationException("Cannot set on a ReadOnlyFragment");
        }

        /// <inheritdoc />
        public Fragment this[int index]
        {
            get {
                var value = ToObject();
                if (!(ToObject() is IArray a)) {
                    return Null;
                }

                if (index < 0 || index >= a.Count) {
                    return Null;
                }

                _parent = value;
                _index = index;
                _key = null;
                return this;
            }
        }

        /// <inheritdoc />
        public Fragment this[string key]
        {
            get {
                Debug.Assert(key != null);
                var value = ToObject();
                if (!(ToObject() is IDictionaryObject)) {
                    return Null;
                }

                _parent = value;
                _key = key;
                return this;
            }
        }

        #endregion

        #region Constructors

        internal Fragment(IDictionaryObject parent, string parentKey)
        {
            _parent = parent;
            _key = parentKey;
        }

        internal Fragment(IArray parent, int index)
        {
            _parent = parent;
            _index = index;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the contained value as a <see cref="ArrayObject"/>
        /// </summary>
        /// <returns>The cast contained value, or <c>null</c></returns>
        public ArrayObject ToArray()
        {
            return ToObject() as ArrayObject;
        }

        /// <summary>
        /// Gets the contained value as a <see cref="Blob"/>
        /// </summary>
        /// <returns>The cast contained value, or <c>null</c></returns>
        public Blob ToBlob()
        {
            return ToObject() as Blob;
        }

        /// <summary>
        /// Gets the contained value as a <see cref="Boolean"/>
        /// </summary>
        /// <returns>The cast contained value</returns>
        /// <remarks>Any non-zero object will be treated as true, so don't rely on 
        /// any sort of parsing</remarks>
        public bool ToBoolean()
        {
            return DataOps.ConvertToBoolean(ToObject());
        }

        /// <summary>
        /// Gets the contained value as a <see cref="DateTimeOffset"/>
        /// </summary>
        /// <returns>The cast contained value, or a default value</returns>
        public DateTimeOffset ToDate()
        {
            return DataOps.ConvertToDate(ToObject());
        }

        /// <summary>
        /// Gets the contained value as a <see cref="DictionaryObject"/>
        /// </summary>
        /// <returns>The cast contained value, or <c>null</c></returns>
        public DictionaryObject ToDictionary()
        {
            return ToObject() as DictionaryObject;
        }

        /// <summary>
        /// Gets the contained value as a <see cref="Double"/>
        /// </summary>
        /// <returns>The cast contained value</returns>
        /// <remarks><c>true</c> will be converted to 1.0, and everything else that
        /// is non-numeric will be 0.0</remarks>
        public double ToDouble()
        {
            return DataOps.ConvertToDouble(ToObject());
        }

        /// <summary>
        /// Gets the contained value as a <see cref="Single"/>
        /// </summary>
        /// <returns>The cast contained value</returns>
        /// <remarks><c>true</c> will be converted to 1.0f, and everything else that
        /// is non-numeric will be 0.0f</remarks>
        public float ToFloat()
        {
            return DataOps.ConvertToFloat(ToObject());
        }

        /// <summary>
        /// Gets the contained value as an <see cref="Int32"/>
        /// </summary>
        /// <returns>The cast contained value</returns>
        /// <remarks><c>true</c> will be converted to 1, a <see cref="Double"/> value
        /// will be rounded, and everything else non-numeric will be 0</remarks>
        public int ToInt()
        {
            return DataOps.ConvertToInt(ToObject());
        }

        /// <summary>
        /// Gets the contained value as an <see cref="Int64"/>
        /// </summary>
        /// <returns>The cast contained value</returns>
        /// <remarks><c>true</c> will be converted to 1, a <see cref="Double"/> value
        /// will be rounded, and everything else non-numeric will be 0</remarks>
        public long ToLong()
        {
            return DataOps.ConvertToLong(ToObject());
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
            if (_parent == null) {
                return null;
            }

            return _key != null
                ? ((IDictionaryObject) _parent).GetObject(_key)
                : ((IArray) _parent).GetObject(_index);
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return ToObject() as string;
        }

        #endregion
    }
}

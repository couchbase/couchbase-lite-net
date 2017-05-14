// 
// Fragment.cs
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

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing an arbitrary entry in a key path
    /// (e.g. object["key"][index]["next_key"], etc)
    /// </summary>
    public sealed class Fragment : ReadOnlyFragment, IDictionaryFragment, IArrayFragment
    {
        #region Variables

        private readonly object _parent;
        private readonly object _parentKey;
        private object _value;

        #endregion

        #region Properties

        /// <inheritdoc />
        public override bool Exists => _value != null;

        /// <inheritdoc />
        public new Fragment this[string key]
        {
            get {
                if (_value is IDictionaryObject d) {
                    return d[key];
                }

                return new Fragment(null, null, null);
            }
        }

        /// <inheritdoc />
        public new Fragment this[int index]
        {
            get {
                if (_value is IArray a) {
                    return a[index];
                }

                return new Fragment(null, null, null);
            }
        }

        /// <summary>
        /// Gets or sets the value of this object
        /// </summary>
        public new object Value
        {
            get => _value;
            set {
                if (_parent is DictionaryObject d) {
                    var key = (string) _parentKey;
                    d.Set(key, value);
                    _value = d.GetObject(key);
                } else if (_parent is ArrayObject a) {
                    var index = (int) _parentKey;
                    try {
                        if (index == a.Count) {
                            a.Add(value);
                        } else {
                            a.Set(index, value);
                        }

                        _value = a.GetObject(index);
                    } catch (Exception) {
                    }
                }
            }
        }

        #endregion

        #region Constructors

        internal Fragment(object value, object parent, object parentKey)
            : base(value)
        {
            _value = value;
            _parent = parent;
            _parentKey = parentKey;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the contained value as an <see cref="ArrayObject"/>
        /// </summary>
        /// <returns>The cast contained value, or <c>null</c></returns>
        public new ArrayObject ToArray()
        {
            return _value as ArrayObject;
        }

        /// <summary>
        /// Gets the contained value as a <see cref="DictionaryObject"/>
        /// </summary>
        /// <returns>The cast contained value, or <c>null</c></returns>
        public new DictionaryObject ToDictionary()
        {
            return _value as DictionaryObject;
        }

        #endregion
    }
}

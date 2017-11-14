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
using System.Diagnostics;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing an arbitrary entry in a key path
    /// (e.g. object["key"][index]["next_key"], etc)
    /// </summary>
    public sealed class MutableFragment : Fragment, IMutableDictionaryFragment, IMutableArrayFragment
    {
        #region Constants

        //private const string Tag = nameof(Fragment);
        public new static readonly MutableFragment Null = new MutableFragment(null, null);

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the value of the fragment
        /// </summary>
        public override object Value
        {
            set {
                if (this == Null) {
                    throw new InvalidOperationException("Specified fragment path does not exist in object, cannot set value");
                }

                if (_parent == null) {
                    return;
                }

                if (_key != null) {
                    ((IMutableDictionary) _parent).Set(_key, value);
                } else {
                    ((IMutableArray) _parent).Set(_index, value);
                }
            }
        }

        /// <inheritdoc />
        public new MutableFragment this[string key]
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

        /// <inheritdoc />
        public new MutableFragment this[int index]
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

        #endregion

        #region Constructors

        internal MutableFragment(IMutableDictionary parent, string key)
            : base(parent, key)
        {
            
        }

        internal MutableFragment(IMutableArray parent, int index)
            : base(parent, index)
        {

        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the contained value as an <see cref="MutableArray"/>
        /// </summary>
        /// <returns>The cast contained value, or <c>null</c></returns>
        public new MutableArray ToArray()
        {
            return ToObject() as MutableArray;
        }

        /// <summary>
        /// Gets the contained value as a <see cref="MutableDictionary"/>
        /// </summary>
        /// <returns>The cast contained value, or <c>null</c></returns>
        public new MutableDictionary ToDictionary()
        {
            return ToObject() as MutableDictionary;
        }

        #endregion
    }
}

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

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed class Fragment : ReadOnlyFragment, IFragment
    {
        #region Variables

        private readonly object _parent;
        private readonly object _parentKey;
        private object _value;

        #endregion

        #region Properties

        public override bool Exists => _value != null;

        public new IFragment this[string key]
        {
            get {
                if (_value is IDictionaryObject d) {
                    return d[key];
                }

                return new Fragment(null, null, null);
            }
        }

        public new IFragment this[int index]
        {
            get {
                if (_value is IArray a) {
                    return a[index];
                }

                return new Fragment(null, null, null);
            }
        }

        public new object Value
        {
            get => _value;
            set {
                if (_parent is IDictionaryObject d) {
                    var key = (string) _parentKey;
                    d.Set(key, value);
                    _value = d.GetObject(key);
                } else if (_parent is IArray a) {
                    var index = (int) _parentKey;
                    try {
                        a.Set(index, value);
                        _value = a.GetObject(index);
                    } catch (Exception) {
                    }
                }
            }
        }

        #endregion

        #region Constructors

        public Fragment(object value, object parent, object parentKey)
            : base(value)
        {
            _value = value;
            _parent = parent;
            _parentKey = parentKey;
        }

        #endregion

        #region IObjectFragment

        public new IArray ToArray()
        {
            return _value as IArray;
        }

        public new ISubdocument ToSubdocument()
        {
            return _value as ISubdocument;
        }

        #endregion
    }
}

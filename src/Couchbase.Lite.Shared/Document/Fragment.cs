﻿// 
//  Fragment.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using System;
using System.Diagnostics;

using Couchbase.Lite.Internal.Logging;

using JetBrains.Annotations;
using Debug = System.Diagnostics.Debug;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed class Fragment : IFragment, IMutableFragment
    {
        #region Constants

        private const string Tag = nameof(Fragment);

        [NotNull]
        public static readonly Fragment Null = new Fragment(null, null);

        #endregion

        #region Variables

        private int _index;
        private string _key;
        private object _parent;

        #endregion

        #region Properties

        IFragment IArrayFragment.this[int index] => GetForIndex(index);

        IFragment IDictionaryFragment.this[string key] => GetForKey(key);

        public Blob Blob
        {
            get => Value as Blob;
            set => Value = value;
        }


        public bool Boolean
        {
            get => DataOps.ConvertToBoolean(Value);
            set => Value = value;
        }

        public DateTimeOffset Date
        {
            get => DataOps.ConvertToDate(Value);
            set => Value = value;
        }

        public double Double
        {
            get => DataOps.ConvertToDouble(Value);
            set => Value = value;
        }

        public bool Exists => Value != null;

        public float Float
        {
            get => DataOps.ConvertToFloat(Value);
            set => Value = value;
        }

        public int Int
        {
            get => DataOps.ConvertToInt(Value);
            set => Value = value;
        }

        public long Long
        {
            get => DataOps.ConvertToLong(Value);
            set => Value = value;
        }

        public string String
        {
            get => Value as string;
            set => Value = value;
        }

        public object Value
        {
            get {
                if (_parent == null) {
                    return null;
                }

                return _key != null
                    ? ((IDictionaryObject) _parent).GetValue(_key)
                    : ((IArray) _parent).GetValue(_index);
            }
            set {
                if (this == Null) {
                    throw new InvalidOperationException(CouchbaseLiteErrorMessage.FragmentPathNotExist);
                }

                if (_parent == null) {
                    WriteLog.To.Database.W(Tag, "Attempt to set value on a parentless fragment, ignoring...");
                    return;
                }

                if (_key != null) {
                    ((IMutableDictionary) _parent).SetValue(_key, value);
                } else {
                    ((IMutableArray) _parent).SetValue(_index, value);
                }
            }

        }

        ArrayObject IFragment.Array => Value as ArrayObject;

        DictionaryObject IFragment.Dictionary => Value as DictionaryObject;

        IMutableFragment IMutableArrayFragment.this[int index] => GetForIndex(index);

        IMutableFragment IMutableDictionaryFragment.this[string key] => GetForKey(key);

        MutableArrayObject IMutableFragment.Array
        {
            get => Value as MutableArrayObject;
            set => Value = value;
        }

        MutableDictionaryObject IMutableFragment.Dictionary
        {
            get => Value as MutableDictionaryObject;
            set => Value = value;
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

        #region Private Methods

        [NotNull]
        private Fragment GetForIndex(int index)
        {
            var value = Value;
            if (!(value is IArray a)) {
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

        [NotNull]
        private Fragment GetForKey([NotNull]string key)
        {
            Debug.Assert(key != null);
            var value = Value;
            if (!(value is IDictionaryObject)) {
                return Null;
            }

            _parent = value;
            _key = key;
            return this;
        }

        #endregion

        #region Overrides

        [CanBeNull]
        public override string ToString() => Value?.ToString();

        #endregion
    }
}

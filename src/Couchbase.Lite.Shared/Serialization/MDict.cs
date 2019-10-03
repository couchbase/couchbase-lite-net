﻿// 
// MDict.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Serialization
{
    internal sealed unsafe class MDict : MCollection
    {
        #region Constants

        private const string Tag = nameof(MDict);

        #endregion

        #region Variables

        private readonly List<string> _newKeys = new List<string>();

        private FLDict* _dict;
        private Dictionary<string, MValue> _map = new Dictionary<string, MValue>();

        #endregion

        #region Properties

        public int Count { get; private set; }

        #endregion

        #region Constructors

        public MDict()
        {
            
        }

        public MDict(MValue mv, MCollection parent)
        {
            InitInSlot(mv, parent);
        }

        #endregion

        #region Public Methods

        public void Clear()
        {
            if (!IsMutable) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.CannotClearNonMutableMDict);
            }

            if (Count == 0) {
                return;
            }

            Mutate();
            _map.Clear();
            foreach (var item in IterateDict()) {
                _map[item.Key] = MValue.Empty;
            }

            Count = 0;
        }

        public bool Contains(string key)
        {
            return _map.ContainsKey(key) || Native.FLDict_Get(_dict, Encoding.UTF8.GetBytes(key)) != null;
        }

        [NotNull]
        public MValue Get([NotNull]string key)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(key), key);

            if (_map.ContainsKey(key)) {
                return _map[key];
            }

            var val = Native.FLDict_Get(_dict, Encoding.UTF8.GetBytes(key));
            if (val == null) {
                return MValue.Empty;
            }

            var retVal = new MValue(val);
            SetInMap(key, retVal);
            return retVal;
        }

        public void InitInSlot(MValue mv, MCollection parent)
        {
            InitInSlot(mv, parent, parent?.MutableChildren == true);
        }

        public void Remove(string key)
        {
            Set(key, MValue.Empty);
        }

        public void Set(string key, MValue val)
        {
            if (!IsMutable) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.CannotSetItemsInNonMutableInMDict);
            }

            if (_map.ContainsKey(key)) {
                var existing = _map[key];
                if (val.IsEmpty && existing.IsEmpty) {
                    return;
                }

                Mutate();
                Count += Convert.ToInt32(!val.IsEmpty) - Convert.ToInt32(!existing.IsEmpty);
                _map[key] = val;
            } else {
                if (Native.FLDict_Get(_dict, Encoding.UTF8.GetBytes(key)) != null) {
                    if (val.IsEmpty) {
                        Count--;
                    }
                } else {
                    Count++;
                }

                Mutate();
                SetInMap(key, val);
            }
        }

        #endregion

        #region Internal Methods

        [NotNull]
        internal IEnumerable<KeyValuePair<string, MValue>> AllItems()
        {
            foreach (var item in _map) {
                if (!item.Value.IsEmpty) {
                    yield return item;
                }
            }

            foreach (var item in IterateDict()) {
                if (!_map.ContainsKey(item.Key)) {
                    yield return item;
                }
            }
        }

        #endregion

        #region Private Methods

        private bool Advance(ref FLDictIterator i)
        {
            fixed (FLDictIterator* i2 = &i) {
                return Native.FLDictIterator_Next(i2);
            }
        }

        private FLDictIterator BeginIteration()
        {
            FLDictIterator i;
            Native.FLDictIterator_Begin(_dict, &i);
            return i;
        }

        private KeyValuePair<string, MValue> Get(FLDictIterator i)
        {
            if (_dict == null || Native.FLDict_Count(_dict) == 0U) {
                return new KeyValuePair<string, MValue>();
            }

            var key = Native.FLDictIterator_GetKeyString(&i);
            if (key == null) {
                return new KeyValuePair<string, MValue>();
            }

            var value = Native.FLDictIterator_GetValue(&i);
            return new KeyValuePair<string, MValue>(key, new MValue(value));
        }

        [NotNull]
        private IEnumerable<KeyValuePair<string, MValue>> IterateDict()
        {
            // I hate this dance...but it's necessary to convince the commpiler to let
            // me use unsafe methods inside of a generator method
            var i = BeginIteration();
            
            do {
                var got = Get(i);
                if (got.Key != null) {
                    yield return got;
                }
            } while (Advance(ref i));
        }

        private void SetInMap(string key, MValue val)
        {
            _newKeys.Add(key);
            _map[key] = val;
        }

        #endregion

        #region Overrides

        public override void FLEncode(FLEncoder* enc)
        {
            if (!IsMutated) {
                if (_dict == null) {
                    Native.FLEncoder_BeginDict(enc,0U);
                    Native.FLEncoder_EndDict(enc);
                } else {
                    Native.FLEncoder_WriteValue(enc, (FLValue*) _dict);
                }
            } else {
                Native.FLEncoder_BeginDict(enc, (uint)Count);
                foreach (var item in _map) {
                    if (item.Value.IsEmpty) {
                        continue;
                    }

                    Native.FLEncoder_WriteKey(enc, item.Key);
                    if (item.Value.HasNative) {
                        item.Value.NativeObject.FLEncode(enc);
                    } else if (item.Value.Value != null) {
                        Native.FLEncoder_WriteValue(enc, item.Value.Value);
                    } else {
                        Native.FLEncoder_WriteNull(enc);
                    }
                }

                foreach (var item in IterateDict()) {
                    if (_map.ContainsKey(item.Key)) {
                        continue;
                    }

                    Native.FLEncoder_WriteKey(enc, item.Key);
                    item.Value.FLEncode(enc);
                }

                Native.FLEncoder_EndDict(enc);
            }
        }

        public override void InitAsCopyOf(MCollection original, bool isMutable)
        {
            base.InitAsCopyOf(original, isMutable);
            var d = original as MDict;
            _dict = d != null ? d._dict : null;
            _map = d?._map;
            Count = d?.Count ?? 0;
        }

        protected override void InitInSlot(MValue slot, MCollection parent, bool isMutable)
        {
            base.InitInSlot(slot, parent, isMutable);
            Debug.Assert(_dict == null);
            _dict = Native.FLValue_AsDict(slot.Value);
            Count = (int)Native.FLDict_Count(_dict);
        }

        #endregion
    }
}
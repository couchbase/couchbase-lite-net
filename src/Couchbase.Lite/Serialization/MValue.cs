// 
// MValue.cs
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
using System.Text;
using Couchbase.Lite.Internal.Doc;
using LiteCore.Interop;
using static LiteCore.Constants;

namespace Couchbase.Lite.Internal.Serialization
{
    internal sealed unsafe class MValue : IDisposable, IFLEncodable
    {
        #region Constants

        public static readonly MValue Empty = new MValue();

        #endregion

        public bool HasNative => NativeObject != null;

        public FLValue* Value { get; private set; }

        public object NativeObject { get; private set; }

        public bool IsEmpty => Value == null && NativeObject == null;

        public bool IsMutated => Value == null;

        #region Constructors

        public MValue()
        {
            
        }

        public MValue(object o)
        {
            NativeObject = o;
        }

        public MValue(FLValue* v)
        {
            Value = v;
        }

        #endregion

        public object AsObject(MCollection parent)
        {
            if (NativeObject != null || Value == null) {
                return NativeObject;
            }

            var cache = false;
            var obj = ToObject(this, parent, ref cache);
            if (cache) {
                NativeObject = obj;
            }

            return obj;
        }

        public void FLEncode(FLEncoder* enc)
        {
            Debug.Assert(!IsEmpty);
            if (Value != null) {
                Native.FLEncoder_WriteValue(enc, Value);
            } else {
                NativeObject.FLEncode(enc);
            }
        }

        public void Mutate()
        {
            Debug.Assert(NativeObject != null);
            Value = null;
        }

        private void NativeChangeSlot(MValue newSlot)
        {
            var collection = CollectionFromObject(NativeObject);
            collection?.SetSlot(newSlot, this);
        }

        private void SetObject(object obj)
        {
            if (NativeObject != obj) {
                if (NativeObject != null) {
                    NativeChangeSlot(null);
                }

                NativeObject = obj;

                if (NativeObject != null) {
                    NativeChangeSlot(this);
                }
            }
        }

        private static object ToObject(MValue mv, MCollection parent, ref bool cache)
        {
            var type = Native.FLValue_GetType(mv.Value);
            switch (type) {
                case FLValueType.Array:
                    cache = true;
                    return parent?.MutableChildren == true ? new ArrayObject(mv, parent) : new ReadOnlyArray(mv, parent);
                case FLValueType.Dict:
                    cache = true;
                    var context = parent.Context as DocContext;
                    var sk = context != null ? context.SharedKeys : null;
                    var flDict = Native.FLValue_AsDict(mv.Value);
                    var subType = Native.FLValue_AsString(Native.FLDict_GetSharedKey(flDict,
                        Encoding.UTF8.GetBytes(ObjectTypeProperty), sk));
                    var obj = CreateSpecialObject(subType, flDict, context);
                    if (obj != null) {
                        return obj;
                    }

                    return parent?.MutableChildren == true
                        ? new DictionaryObject(mv, parent)
                        : new ReadOnlyDictionary(mv, parent);
                default:
                    return FLSliceExtensions.ToObject(mv.Value);
            }
        }

        private static object CreateSpecialObject(string type, FLDict* properties, DocContext context)
        {
            Debug.Assert(context != null);
            return type == ObjectTypeBlob || FLValueConverter.IsOldAttachment(context.Db, properties)
                ? context.ToObject((FLValue*) properties, true)
                : null;
        }

        private static MCollection CollectionFromObject(object obj)
        {
            switch (obj) {
                case ReadOnlyArray arr:
                    return arr.ToMCollection();
                case ReadOnlyDictionary dict:
                    return dict.ToMCollection();
                default:
                    return null;
            }
        }

        #region IDisposable

        public void Dispose()
        {
            if (NativeObject != null) {
                NativeChangeSlot(null);
            }
        }

        #endregion
    }
}
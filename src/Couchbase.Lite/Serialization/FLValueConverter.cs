﻿// 
// FLValueConverter.cs
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
using System.Collections.Generic;
using System.Reflection;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Serialization
{
    internal static unsafe class FLValueConverter
    {
        #region Constants

        private const string Tag = nameof(FLValueConverter);

        #endregion

        #region Public Methods

        public static object ToCouchbaseObject(FLValue* value, Database database, bool dotNetTypes, Type hintType1 = null)
        {
                switch (Native.FLValue_GetType(value)) {
                    case FLValueType.Array: {
                        if(dotNetTypes) {
                            return ToObject(value, database.SharedStrings, 0, hintType1);
                        }

                        var array = new ArrayObject(new MArray(new MValue(value), null), false);
                        return array;
                    }
                    case FLValueType.Dict: {
                        var dict = Native.FLValue_AsDict(value);
                        var type = TypeForDict(dict, database.SharedStrings);
                        if (!dotNetTypes && type.buf == null && !IsOldAttachment(database, dict)) {
                            return new DictionaryObject(new MDict(new MValue(value), null), false);
                        }

                        var result = ToObject(value, database.SharedStrings, 0, hintType1) as IDictionary<string, object>;
                        return ConvertDictionary(result, database);
                    }
                    case FLValueType.Undefined:
                        return null;
                    default:
                        return ToObject(value, database.SharedStrings);
                }
        }

        #endregion

        #region Internal Methods

        internal static bool FLEncode(object obj, FLEncoder* enc)
        {
            switch (obj) {
                case ArrayObject arObj:
                    arObj.ToMCollection().FLEncode(enc);
                    return true;
                case DictionaryObject roDict:
                    roDict.ToMCollection().FLEncode(enc);
                    return true;
                case Blob b:
                    b.FLEncode(enc);
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsOldAttachment(Database db, FLDict* dict)
        {
            var flDigest = db.SharedStrings.GetDictValue(dict, "digest");
            var flLength = db.SharedStrings.GetDictValue(dict, "length");
            var flStub = db.SharedStrings.GetDictValue(dict, "stub");
            var flRevPos = db.SharedStrings.GetDictValue(dict, "revpos");
            var flContentType = db.SharedStrings.GetDictValue(dict, "content_type");

            return flDigest != null && flLength != null && flStub != null && flRevPos != null && flContentType != null;
        }

        #endregion

        #region Private Methods

        private static object ConvertDictionary(IDictionary<string, object> dict, Database database)
        {
            var type = dict.GetCast<string>(Constants.ObjectTypeProperty);
            if (type == null) {
                if(IsOldAttachment(dict)) {
                    return new Blob(database, dict);
                }

                return dict;
            }

            if(type == Constants.ObjectTypeBlob) {
                return new Blob(database, dict);
            }

            return dict;
        }

        private static bool IsOldAttachment(IDictionary<string, object> dict)
        {
            var digest = dict.Get("digest");
            var length = dict.Get("length");
            var stub = dict.Get("stub");
            var revpos = dict.Get("revpos");
            var contentType = dict.Get("content_type");
            return digest != null && length != null && stub != null && revpos != null && contentType != null;
        }

        private static object ToObject(FLValue* value, SharedStringCache sharedKeys, int level = 0, Type hintType1 = null)
        {
            if (value == null) {
                return null;
            }

            switch (Native.FLValue_GetType(value)) {
                case FLValueType.Array: {
                    var arr = Native.FLValue_AsArray(value);
                    var hintType = level == 0 && hintType1 != null ? hintType1 : typeof(object);
                    var count = (int)Native.FLArray_Count(arr);
                    if (count == 0) {
                        return new List<object>();
                    }

                    var retVal =
                        (IList)Activator.CreateInstance(typeof(List<>).GetTypeInfo().MakeGenericType(hintType),
                            count);

                    var i = default(FLArrayIterator);
                    Native.FLArrayIterator_Begin(arr, &i);
                    do {
                        retVal.Add(ToObject(Native.FLArrayIterator_GetValue(&i), sharedKeys, level + 1, hintType1));
                    } while (Native.FLArrayIterator_Next(&i));

                    return retVal;
                }
                case FLValueType.Boolean:
                    return Native.FLValue_AsBool(value);
                case FLValueType.Data:
                    return Native.FLValue_AsData(value);
                case FLValueType.Dict: {
                    var dict = Native.FLValue_AsDict(value);
                    var hintType = level == 0 && hintType1 != null ? hintType1 : typeof(object);
                    var count = (int)Native.FLDict_Count(dict);
                    if (count == 0) {
                        return new Dictionary<string, object>();
                    }

                    var retVal = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), hintType), count);
                    var i = default(FLDictIterator);
                    Native.FLDictIterator_Begin(dict, &i);
                    do {
                        var rawKey = Native.FLDictIterator_GetKey(&i);
                        string key;
                        if (Native.FLValue_GetType(rawKey) == FLValueType.Number) {
                            key = sharedKeys.GetKey((int)Native.FLValue_AsInt(rawKey));
                            if (key == null) {
                                Log.To.Database.W(Tag, "Corrupt key found during deserialization, skipping...");
                                continue;
                            }
                        } else {
                            key = Native.FLValue_AsString(rawKey);
                        }

                        retVal[key] = ToObject(Native.FLDictIterator_GetValue(&i), sharedKeys, level + 1, hintType1);
                    } while (Native.FLDictIterator_Next(&i));

                    return retVal;
                }
                case FLValueType.Null:
                    return null;
                case FLValueType.Number:
                    if(Native.FLValue_IsInteger(value)) {
                        if(Native.FLValue_IsUnsigned(value)) {
                            return Native.FLValue_AsUnsigned(value);
                        }

                        return Native.FLValue_AsInt(value);
                    } else if(Native.FLValue_IsDouble(value)) {
                        return Native.FLValue_AsDouble(value);
                    }

                    return Native.FLValue_AsFloat(value);
                case FLValueType.String:
                    return Native.FLValue_AsString(value);
                default:
                    return null;
            }
        }

        private static FLSlice TypeForDict(FLDict* dict, SharedStringCache sharedKeys)
        {
            var typeKey = FLSlice.Constant(Constants.ObjectTypeProperty);
            var type = sharedKeys != null
                ? sharedKeys.GetDictValue(dict, typeKey)
                : NativeRaw.FLDict_Get(dict, typeKey);

            return NativeRaw.FLValue_AsString(type);
        }

        #endregion
    }
}

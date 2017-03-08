// 
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
using System.Collections.Generic;

using Couchbase.Lite.Logging;
using LiteCore.Interop;

namespace Couchbase.Lite.Serialization
{
    internal static unsafe class FLValueConverter
    {
        #region Constants

        private const string Tag = nameof(FLValueConverter);

        #endregion

        #region Public Methods

        public static object ToObject(FLValue* value, SharedStringCache sharedKeys)
        {
            if (value == null) {
                return null;
            }

            switch (Native.FLValue_GetType(value)) {
                case FLValueType.Array: {
                        var arr = Native.FLValue_AsArray(value);
                        var retVal = new object[Native.FLArray_Count(arr)];
                        var i = default(FLArrayIterator);
                        Native.FLArrayIterator_Begin(arr, &i);
                        int pos = 0;
                        do {
                            retVal[pos++] = ToObject(Native.FLArrayIterator_GetValue(&i), sharedKeys);
                        } while (Native.FLArrayIterator_Next(&i));

                        return retVal;
                    }
                case FLValueType.Boolean:
                    return Native.FLValue_AsBool(value);
                case FLValueType.Data:
                    return Native.FLValue_AsData(value);
                case FLValueType.Dict: {
                        var dict = Native.FLValue_AsDict(value);
                        var retVal = new Dictionary<string, object>((int)Native.FLDict_Count(dict));
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

                            retVal[key] = ToObject(Native.FLDictIterator_GetValue(&i), sharedKeys);
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
        
        #endregion
    }
}

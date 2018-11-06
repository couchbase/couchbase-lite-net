// 
// DataOps.cs
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
using System.Globalization;
using System.Linq;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Interop;

using LiteCore.Interop;
using Newtonsoft.Json.Linq;

namespace Couchbase.Lite.Internal.Doc
{
    internal static class DataOps
    {
        #region Internal Methods

        internal static bool ConvertToBoolean(object value)
        {
            switch (value) {
                case null:
                    return false;
                case string s:
                    return true; // string is IConvertible, but will throw on things other than true or false
                case IConvertible c:
                    return c.ToBoolean(CultureInfo.InvariantCulture);
                default:
                    return true;
            }
        }

        internal static DateTimeOffset ConvertToDate(object value)
        {
            switch (value) {
                case null:
                    return DateTimeOffset.MinValue;
                case DateTimeOffset dto:
                    return dto;
                case DateTime dt:
                    return new DateTimeOffset(dt.ToUniversalTime());
                case string s:
                    if (DateTimeOffset.TryParseExact(s, "o", CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out var retVal)) {
                        return retVal;
                    }

                    return DateTimeOffset.MinValue;
                default:
                    return DateTimeOffset.MinValue;
            }
        }

        internal static double ConvertToDouble(object value)
        {
            // NOTE: Cannot use ConvertToDecimal because double has a greater range
            switch (value) {
                case string s: // string is IConvertible, but will throw for non-numeric strings
                    return 0.0;
                case IConvertible c:
                    return c.ToDouble(CultureInfo.InvariantCulture);
                default:
                    return 0.0;
            }
        }

        internal static float ConvertToFloat(object value)
        {
            // NOTE: Cannot use ConvertToDecimal because float has a greater range
            switch (value) {
                case string s: // string is IConvertible, but will throw for non-numeric strings
                    return 0.0f;
                case IConvertible c:
                    return c.ToSingle(CultureInfo.InvariantCulture);
                default:
                    return 0.0f;
            }
        }

        internal static int ConvertToInt(object value)
        {
            return (int)Math.Truncate(ConvertToDecimal(value));
        }

        internal static long ConvertToLong(object value)
        {
            return (long)Math.Truncate(ConvertToDecimal(value));
        }

        internal static object ToCouchbaseObject(object value)
        {
            switch (value) {
                case null:
                    return null;
                case DateTimeOffset dto:
                    return dto.ToString("o");
                case DictionaryObject rodic when !(rodic is MutableDictionaryObject):
                    return rodic.ToMutable();
                case ArrayObject roarr when !(roarr is MutableArrayObject):
                    return roarr.ToMutable();
                case JObject jobj:
                    return ConvertDictionary(jobj.ToObject<IDictionary<string, object>>());
                case JArray jarr:
                    return ConvertList(jarr.ToObject<IList>());
                case JToken jToken:
                    switch (jToken.Type) {
                        case JTokenType.Date:
                            return (DateTimeOffset) jToken;
                        default:
                            return jToken.ToObject<object>();
                    }
                case IDictionary<string, object> dict:
                    return ConvertDictionary(dict);
                case IList list:
                    return ConvertList(list);
                case byte b:
                case sbyte sb:
                case ushort us:
                case short s:
                case uint ui:
                case int i:
                case long l:
                case string str:
                case bool bl:
                case float f:
                case double d:
                case MutableArrayObject ao:
                case MutableDictionaryObject dict:
                case Blob blob:
                    return value;
                default:
                    throw new ArgumentException($"{value.GetType().Name} is not a valid type. " +
                                                "You may only pass byte, sbyte, short, ushort, int, uint, long, ulong, float, double, bool, DateTimeOffset, Blob, " +
                                                "or one-dimensional arrays or dictionaries containing the above types");
            }
        }

        internal static object ToNetObject(object value)
        {
            switch (value) {
                case null:
                    return null;
                case InMemoryDictionary inMem:
                    return inMem.ToDictionary();
                case IDictionaryObject roDic:
                    return roDic.ToDictionary();
                case IArray roarr:
                    return roarr.ToList();
                default:
                    return value;
            }
        }

        internal static unsafe bool ValueWouldChange(object newValue, MValue oldValue, MCollection container)
        {
            // As a simplification we assume that array and fict values are always different, to avoid
            // a possibly expensive comparison
            var oldType = Native.FLValue_GetType(oldValue.Value);
            if (oldType == FLValueType.Undefined || oldType == FLValueType.Dict || oldType == FLValueType.Array) {
                return true;
            }

            switch (newValue) {
                case ArrayObject arr:
                case DictionaryObject dict:
                    return true;
                default:
                    var oldVal = oldValue.AsObject(container);
                    return !newValue?.Equals(oldVal) ?? oldVal != null;
            }
        }

        #endregion

        #region Private Methods

        private static MutableDictionaryObject ConvertDictionary(IDictionary<string, object> dictionary)
        {
            var subdocument = new MutableDictionaryObject();
            subdocument.SetData(dictionary);
            return subdocument;
        }

        private static MutableArrayObject ConvertList(IList list)
        {
            var array = new MutableArrayObject();
            array.SetData(list);
            return array;
        }

        private static decimal ConvertToDecimal(object value)
        {
            switch (value) {
                case string s: // string is IConvertible, but will throw for non-numeric strings
                    return 0;
                case IConvertible c:
                    return c.ToDecimal(CultureInfo.InvariantCulture);
                default:
                    return 0;
            }
        }

        #endregion
    }
}

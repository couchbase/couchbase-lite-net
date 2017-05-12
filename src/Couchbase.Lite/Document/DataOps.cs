// 
// DataOps.cs
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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json.Linq;

namespace Couchbase.Lite.Internal.Doc
{
    internal static class DataOps
    {
        #region Constants

        private static readonly TypeInfo[] ValidTypes = {
            typeof(string).GetTypeInfo(),
            typeof(DateTimeOffset).GetTypeInfo(),
            typeof(Blob).GetTypeInfo(),
            typeof(IReadOnlyArray).GetTypeInfo(),
            typeof(IReadOnlyDictionary).GetTypeInfo(),
            typeof(IDictionary<string,object>).GetTypeInfo(),
            typeof(IList<>).GetTypeInfo()
        };

        #endregion

        #region Internal Methods

        internal static bool ContainsBlob(object value)
        {
            switch (value) {
                case Blob b:
                    return true;
                case string s:
                    return false;
                case IEnumerable<KeyValuePair<string, object>> s:
                    return ContainsBlob(s);
                case IEnumerable a:
                    return ContainsBlob(a);
                default:
                    return false;
            }
        }

        internal static bool ContainsBlob(IEnumerable<KeyValuePair<string, object>> s)
        {
            foreach (var pair in s) {
                if (ContainsBlob(pair.Value)) {
                    return true;
                }
            }

            return false;
        }

        internal static bool ContainsBlob(IEnumerable a)
        {
            foreach(var obj in a) {
                if (ContainsBlob(obj)) {
                    return true;
                }
            }

            return false;
        }

        internal static ArrayObject ConvertArray(ArrayObject array, EventHandler<ObjectChangedEventArgs<ArrayObject>> callback)
        {
            array.Changed += callback;
            return array;
        }

        internal static DictionaryObject ConvertDictionary(IDictionary<string, object> dictionary, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback)
        {
            var subdocument = new DictionaryObject();
            subdocument.Set(dictionary);
            subdocument.Changed += callback;
            return subdocument;
        }

        internal static DictionaryObject ConvertDictionaryObject(DictionaryObject subdoc, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback)
        {
            subdoc.Changed += callback;
            return subdoc;
        }

        internal static ArrayObject ConvertList(IList list, EventHandler<ObjectChangedEventArgs<ArrayObject>> callback)
        {
            var array = new ArrayObject();
            array.Set(list);
            array.Changed += callback;
            return array;
        }

        internal static ArrayObject ConvertROArray(ReadOnlyArray readOnlyArray, EventHandler<ObjectChangedEventArgs<ArrayObject>> callback)
        {
            var array = new ArrayObject(readOnlyArray.Data);
            array.Changed += callback;
            return array;
        }

        internal static DictionaryObject ConvertRODictionary(ReadOnlyDictionary readOnlySubdoc, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback)
        {
            var subdocument = new DictionaryObject(readOnlySubdoc.Data);
            subdocument.Changed += callback;
            return subdocument;
        }

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
                case string s:
                    DateTimeOffset retVal;
                    if (DateTimeOffset.TryParseExact(s, "o", CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out retVal)) {
                        return retVal;
                    }

                    return DateTimeOffset.MinValue;
                default:
                    return DateTimeOffset.MinValue;
            }
        }

        internal static double ConvertToDouble(object value)
        {
            switch (value) {
                case string s: // string is IConvertible, but will throw for non-numeric strings
                    return 0.0;
                case IConvertible c:
                    return c.ToDouble(CultureInfo.InvariantCulture);
                default:
                    return 0.0;
            }
        }

        internal static int ConvertToInt(object value)
        {
            switch (value) {
                case string s:
                    return 0; // string is IConvertible, but will throw for non-numeric strings
                case IConvertible c:
                    return c.ToInt32(CultureInfo.InvariantCulture);
                default:
                    return 0;
            }
        }

        internal static long ConvertToLong(object value)
        {
            switch (value) {
                case string s:
                    return 0L; // string is IConvertible, but will throw for non-numeric strings
                case IConvertible c:
                    return c.ToInt64(CultureInfo.InvariantCulture);
                default:
                    return 0L;
            }
        }

        internal static object ConvertValue(object value, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback1,
             EventHandler<ObjectChangedEventArgs<ArrayObject>> callback2)
        {
            switch (value) {
                case null:
                    return null;
                case DateTimeOffset dto:
                    return dto.ToString("o");
                case DictionaryObject subdoc:
                    return ConvertDictionaryObject(subdoc, callback1);
                case ArrayObject arr:
                    return ConvertArray(arr, callback2);
                case ReadOnlyDictionary rosubdoc:
                    return ConvertRODictionary(rosubdoc, callback1);
                case ReadOnlyArray roarr:
                    return ConvertROArray(roarr, callback2);
                case JObject jobj:
                    return ConvertDictionary(jobj.ToObject<IDictionary<string, object>>(), callback1);
                case JArray jarr:
                    return ConvertList(jarr.ToObject<IList>(), callback2);
                case IDictionary<string, object> dict:
                    return ConvertDictionary(dict, callback1);
                case IList list:
                    return ConvertList(list, callback2);
                default:
                    return value;
            }
        }

        [Conditional("DEBUG")]
        internal static void ValidateValue(object value)
        {
            if (value == null) {
                return;
            }

            var type = value.GetType();
            if (IsValidScalarType(type)) {
                return;
            }

            var jType = value as JToken;
            if (jType != null) {
                if (jType.Type == JTokenType.Object || jType.Type == JTokenType.Array) {
                    return;
                }

                throw new ArgumentException($"Invalid type in document properties: {type.Name}", nameof(value));
            }
        }

        #endregion

        #region Private Methods

        private static bool IsValidScalarType(Type type)
        {
            var info = type.GetTypeInfo();
            if (info.IsPrimitive) {
                return true;
            }

            return ValidTypes.Any(x => info.IsAssignableFrom(x));
        }

        #endregion
    }
}

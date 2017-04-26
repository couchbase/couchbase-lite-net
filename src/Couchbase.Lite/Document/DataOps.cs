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

        internal static object ConvertValue(object value, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback1,
             EventHandler<ObjectChangedEventArgs<ArrayObject>> callback2)
        {
            switch (value) {
                case null:
                    return null;
                case Subdocument subdoc:
                    return ConvertSubdocument(subdoc, callback1);
                case ArrayObject arr:
                    return ConvertArray(arr, callback2);
                case ReadOnlySubdocument rosubdoc:
                    return ConvertROSubdocument(rosubdoc, callback1);
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

        internal static Subdocument ConvertSubdocument(Subdocument subdoc, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback)
        {
            subdoc.Dictionary.Changed += callback;
            return subdoc;
        }

        internal static ArrayObject ConvertArray(ArrayObject array, EventHandler<ObjectChangedEventArgs<ArrayObject>> callback)
        {
            array.Changed += callback;
            return array;
        }

        internal static Subdocument ConvertDictionary(IDictionary<string, object> dictionary, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback)
        {
            var subdocument = new Subdocument();
            subdocument.Set(dictionary);
            subdocument.Dictionary.Changed += callback;
            return subdocument;
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

        internal static Subdocument ConvertROSubdocument(ReadOnlySubdocument readOnlySubdoc, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback)
        {
            var converted = readOnlySubdoc as ReadOnlySubdocument ?? throw new InvalidOperationException("Custom IReadOnlySubdocument not supported");
            var subdocument = new Subdocument(converted.Data);
            subdocument.Dictionary.Changed += callback;
            return subdocument;
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

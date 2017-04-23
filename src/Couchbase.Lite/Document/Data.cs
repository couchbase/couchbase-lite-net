// 
// Data.cs
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
    internal static class Data
    {
        #region Constants

        private static readonly TypeInfo[] ValidTypes = {
            typeof(string).GetTypeInfo(),
            typeof(DateTimeOffset).GetTypeInfo(),
            typeof(IReadOnlySubdocument).GetTypeInfo(),
            typeof(IBlob).GetTypeInfo(),
            typeof(IReadOnlyArray).GetTypeInfo(),
            typeof(IDictionary<string,object>).GetTypeInfo(),
            typeof(IList<>).GetTypeInfo()
        };

        #endregion

        #region Internal Methods

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

            if (value is ISubdocument || value is IBlob) {
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
                case ISubdocument subdoc:
                    return ConvertSubdocument(subdoc, callback1);
                case IArray arr:
                    return ConvertArray(arr, callback2);
                case IReadOnlySubdocument rosubdoc:
                    return ConvertROSubdocument(rosubdoc, callback1);
                case IReadOnlyArray roarr:
                    return ConvertROArray(roarr, callback2);
                case IDictionary<string, object> dict:
                    return ConvertDictionary(dict, callback1);
                case IList list:
                    return ConvertList(list, callback2);
                default:
                    return value;
            }
        }

        internal static ISubdocument ConvertSubdocument(ISubdocument subdoc, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback)
        {
            var converted = subdoc as Subdocument ?? throw new InvalidOperationException("Custom ISubdocument not supported");
            converted.Dictionary.Changed += callback;
            return subdoc;
        }

        internal static IArray ConvertArray(IArray array, EventHandler<ObjectChangedEventArgs<ArrayObject>> callback)
        {
            var converted = array as ArrayObject ?? throw new InvalidOperationException("Custom IArray not supported");
            converted.Changed += callback;
            return array;
        }

        internal static ISubdocument ConvertDictionary(IDictionary<string, object> dictionary, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback)
        {
            var subdocument = new Subdocument();
            subdocument.Set(dictionary);
            subdocument.Dictionary.Changed += callback;
            return subdocument;
        }

        internal static IArray ConvertList(IList list, EventHandler<ObjectChangedEventArgs<ArrayObject>> callback)
        {
            var array = new ArrayObject();
            array.Set(list);
            array.Changed += callback;
            return array;
        }

        internal static IArray ConvertROArray(IReadOnlyArray readOnlyArray, EventHandler<ObjectChangedEventArgs<ArrayObject>> callback)
        {
            var converted = readOnlyArray as ReadOnlyArray ?? throw new InvalidOperationException("Custom IReadOnlyArray not supported");
            var array = new ArrayObject(converted.Data);
            array.Changed += callback;
            return array;
        }

        internal static ISubdocument ConvertROSubdocument(IReadOnlySubdocument readOnlySubdoc, EventHandler<ObjectChangedEventArgs<DictionaryObject>> callback)
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

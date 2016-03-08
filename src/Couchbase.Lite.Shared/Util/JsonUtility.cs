//
// JsonUtility.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Collections;

namespace Couchbase.Lite.Util
{
    internal static class JsonUtility
    {
        public static object ConvertToNetObject(object jsonObject)
        {
            var dictionaryAttempt = jsonObject.AsDictionary<string, object>();
            if (dictionaryAttempt != null) {
                var retVal = new Dictionary<string, object>();
                foreach (var pair in dictionaryAttempt) {
                    retVal[pair.Key] = ConvertToNetObject(pair.Value);
                }

                return retVal;
            }

            var arrayAttempt = jsonObject.AsList<object>();
            if (arrayAttempt != null) {
                var retVal = new List<object>();
                foreach (var item in arrayAttempt) {
                    retVal.Add(ConvertToNetObject(item));
                }

                return retVal;
            }

            return jsonObject;
        }

        public static T ConvertToNetObject<T>(object jsonObject)
        {
            return (T)ConvertToNetObject(jsonObject);
        }

        public static void PopulateNetObject(object jsonObject, object template)
        {
            PopulateNetObject_Internal(jsonObject, template);
        }

        private static object PopulateNetObject_Internal(object jsonObject, object template)
        {
            var dictionaryAttempt = template as IDictionary;
            if (dictionaryAttempt != null) {
                var jsonDictionary = jsonObject.AsDictionary<string, object>();
                if (jsonDictionary == null) {
                    Log.To.NoDomain.E("JsonUtility", "JsonTypeHints specified an object, " +
                        "but json portion is not an object ({0})", jsonObject.GetType().Name);
                    throw new InvalidOperationException("Invalid JsonTypeHints");
                }

                foreach (var pair in jsonDictionary) {
                    var innerObject = Activator.CreateInstance(dictionaryAttempt.GetType().GetGenericArguments()[1]);
                    dictionaryAttempt[pair.Key] = PopulateNetObject_Internal(pair.Value, innerObject);
                }

                return dictionaryAttempt;
            }

            var arrayAttempt = template as IList;
            if (arrayAttempt != null) {
                var jsonArray = jsonObject.AsList<object>();
                if (jsonArray == null) {
                    Log.To.NoDomain.E("JsonUtility", "JsonTypeHints specified an array, " +
                        "but json portion is not an array ({0})", jsonObject.GetType().Name);
                    throw new InvalidOperationException("Invalid JsonTypeHints");
                }


                foreach (var item in jsonArray) {
                    var innerObject = Activator.CreateInstance(arrayAttempt.GetType().GetGenericArguments()[0]);
                    arrayAttempt.Add(PopulateNetObject_Internal(item, innerObject));
                }

                return arrayAttempt;
            }

            return jsonObject;
        }
    }
}


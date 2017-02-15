// 
//  Extensions.cs
// 
//  Author:
//  Jim Borden  <jim.borden@couchbase.com>
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
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Couchbase.Lite.Util
{
    public static class Extensions
    {
        #region Public Methods

        public static T CastOrDefault<T>(object obj, T defaultVal)
        {
            T retVal;
            if(obj != null && TryCast(obj, out retVal)) {
                return retVal;
            }

            return defaultVal;
        }

        public static T CastOrDefault<T>(object obj)
        {
            return CastOrDefault(obj, default(T));
        }

        public static TValue Get<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key)
        {
            TValue val;
            d.TryGetValue(key, out val);
            return val;
        }

        public static TValue Get<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> d, TKey key)
        {
            TValue val;
            d.TryGetValue(key, out val);
            return val;
        }

        public static T GetCast<T>(this IDictionary<string, object> collection, string key)
        {
            return GetCast(collection, key, default(T));
        }

        public static T GetCast<T>(this IDictionary<string, object> collection, string key, T defaultVal)
        {
            var value = Get(collection, key);
            return CastOrDefault(value, defaultVal);
        }

        public static T GetCast<T>(this IReadOnlyDictionary<string, object> collection, string key)
        {
            return GetCast(collection, key, default(T));
        }

        public static T GetCast<T>(this IReadOnlyDictionary<string, object> collection, string key, T defaultVal)
        {
            var value = Get(collection, key);
            return CastOrDefault(value, defaultVal);
        }

        public static string ReplaceAll(this string str, string regex, string replacement)
        {
            var rgx = new Regex(regex);

            if (replacement.IndexOfAny(new[] {'\\', '$'}) == -1) {
                return rgx.Replace(str, replacement);
            }

            // Back references not yet supported
            var sb = new StringBuilder();
            for(var n = 0; n < replacement.Length; n++) {
                var c = replacement[n];
                switch (c) {
                    case '$':
                        throw new NotSupportedException("Back references not supported");
                    case '\\':
                        c = replacement[++n];
                        break;
                }
                sb.Append(c);
            }
            replacement = sb.ToString();

            return rgx.Replace(str, replacement);
        }

        public static bool TryCast<T>(object obj, out T castVal)
        {
            //If the types already match then things are easy
            if(obj is T) {
                castVal = (T)obj;
                return true;
            }

            try {
                //Take the slow route for things like boxed value types
                castVal = (T)Convert.ChangeType(obj, typeof(T));
            } catch(Exception) {
                castVal = default(T);
                return false;
            }

            return true;
        }

        public static bool TryGetValue<T>(this IDictionary<string, object> dic, string key, out T value)
        {
            value = default(T);
            object obj;
            if(!dic.TryGetValue(key, out obj)) {
                return false;
            }

            //If the types already match then things are easy
            if((obj is T)) {
                value = (T)obj;
                return true;
            }

            try {
                //Take the slow route for things like boxed value types
                value = (T)Convert.ChangeType(obj, typeof(T));
                return true;
            } catch(Exception) {
                return false;
            }
        }

        public static bool TryGetValue<T>(this IReadOnlyDictionary<string, object> dic, string key, out T value)
        {
            value = default(T);
            object obj;
            if(!dic.TryGetValue(key, out obj)) {
                return false;
            }

            //If the types already match then things are easy
            if((obj is T)) {
                value = (T)obj;
                return true;
            }

            try {
                //Take the slow route for things like boxed value types
                value = (T)Convert.ChangeType(value, typeof(T));
                return true;
            } catch(Exception) {
                return false;
            }
        }

        #endregion
    }
}

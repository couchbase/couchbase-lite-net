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
    /// <summary>
    /// A collection of helpful extensions
    /// </summary>
    public static class Extensions
    {
        #region Public Methods

        /// <summary>
        /// Attempts to cast an object to a given type, and returning a default value if not successful
        /// </summary>
        /// <typeparam name="T">The type to cast the object to</typeparam>
        /// <param name="obj">The object to cast</param>
        /// <param name="defaultVal">The default value to use on failure</param>
        /// <returns>The cast object, or <c>defaultVal</c> if not successful</returns>
        public static T CastOrDefault<T>(object obj, T defaultVal = default(T))
        {
            if(obj != null && TryCast(obj, out T retVal)) {
                return retVal;
            }

            return defaultVal;
        }

        /// <summary>
        /// Attempts to get the value for a given key from a dictionary, returning the compiler
        /// default value if not successful
        /// </summary>
        /// <typeparam name="TKey">The key type of the dictionary</typeparam>
        /// <typeparam name="TValue">The value type of the dictionary</typeparam>
        /// <param name="d">The dictionary to operate on (implicit)</param>
        /// <param name="key">The key to attempt to retrieve the value for</param>
        /// <returns>The value for the given key, or a default value</returns>
        public static TValue Get<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key)
        {
            d.TryGetValue(key, out var val);
            return val;
        }

        /// <summary>
        /// Attempts to get the value for a given key from a dictionary, returning the compiler
        /// default value if not successful
        /// </summary>
        /// <typeparam name="TKey">The key type of the dictionary</typeparam>
        /// <typeparam name="TValue">The value type of the dictionary</typeparam>
        /// <param name="d">The dictionary to operate on (implicit)</param>
        /// <param name="key">The key to attempt to retrieve the value for</param>
        /// <returns>The value for the given key, or a default value</returns>
        public static TValue Get<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> d, TKey key)
        {
            d.TryGetValue(key, out var val);
            return val;
        }

        /// <summary>
        /// Gets the value for the given key as the given type, or a default value
        /// if the value does not exist or is the incorrect type
        /// </summary>
        /// <typeparam name="T">The type to cast the result to</typeparam>
        /// <param name="collection">The dictionary to operate on (implicit)</param>
        /// <param name="key">The key to attempt to retrieve the value for</param>
        /// <param name="defaultVal">The value to return on failure</param>
        /// <returns>The fetched value, or the compiler default value if not successful</returns>
        public static T GetCast<T>(this IDictionary<string, object> collection, string key, T defaultVal = default(T))
        {
            var value = Get(collection, key);
            return CastOrDefault(value, defaultVal);
        }

        /// <summary>
        /// Gets the value for the given key as the given type, or a default value
        /// if the value does not exist or is the incorrect type
        /// </summary>
        /// <typeparam name="T">The type to cast the result to</typeparam>
        /// <param name="collection">The dictionary to operate on (implicit)</param>
        /// <param name="key">The key to attempt to retrieve the value for</param>
        /// <param name="defaultVal">The value to return on failure</param>
        /// <returns>The fetched value, or the compiler default value if not successful</returns>
        public static T GetCast<T>(this IReadOnlyDictionary<string, object> collection, string key, T defaultVal = default(T))
        {
            var value = Get(collection, key);
            return CastOrDefault(value, defaultVal);
        }

        /// <summary>
        /// Replaces all instances of a given regex with the given replacement
        /// </summary>
        /// <param name="str">The string to operate on (implicit)</param>
        /// <param name="regex">The regex string to search for</param>
        /// <param name="replacement">The replacement value to use</param>
        /// <returns>A string with all of the given regex expression matches replaced with <c>replacement</c></returns>
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

        /// <summary>
        /// Attempts to cast an object to a given type
        /// </summary>
        /// <typeparam name="T">The type to cast to</typeparam>
        /// <param name="obj">The object to operate on</param>
        /// <param name="castVal">An out value containing the cast object</param>
        /// <returns><c>true</c> if the object was cast, otherwise <c>false</c></returns>
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

        /// <summary>
        /// Tries to get the value for the given key as the given type
        /// </summary>
        /// <typeparam name="T">The type to get the value as</typeparam>
        /// <param name="dic">The dictionary to operate on (implicit)</param>
        /// <param name="key">The key to attempt to retrieve the value for</param>
        /// <param name="value">The out parameter containing the cast value</param>
        /// <returns><c>true</c> if the value was found and cast, <c>false</c> otherwise</returns>
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

        /// <summary>
        /// Tries to get the value for the given key as the given type
        /// </summary>
        /// <typeparam name="T">The type to get the value as</typeparam>
        /// <param name="dic">The dictionary to operate on (implicit)</param>
        /// <param name="key">The key to attempt to retrieve the value for</param>
        /// <param name="value">The out parameter containing the cast value</param>
        /// <returns><c>true</c> if the value was found and cast, <c>false</c> otherwise</returns>
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
                value = (T)Convert.ChangeType(obj, typeof(T));
                return true;
            } catch(Exception) {
                return false;
            }
        }

        #endregion
    }
}

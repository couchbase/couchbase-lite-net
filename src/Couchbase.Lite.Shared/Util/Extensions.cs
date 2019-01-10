// 
//  Extensions.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Couchbase.Lite.Sync;

using LiteCore.Interop;

namespace Couchbase.Lite.Util
{
    /// <summary>
    /// A collection of helpful extensions
    /// </summary>
    internal static class Extensions
    {
        #region Public Methods

        public static DocumentFlags ToDocumentFlags(this C4RevisionFlags flags)
        {
            var retVal = (DocumentFlags)0;
            if (flags.HasFlag(C4RevisionFlags.Deleted)) {
                retVal |= DocumentFlags.Deleted;
            }

            if (flags.HasFlag(C4RevisionFlags.Purged)) {
                retVal |= DocumentFlags.AccessRemoved;
            }

            return retVal;
        }

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
        /// Gets the appropriate numeric type for comparing two untyped objects
        /// </summary>
        /// <param name="left">The lefthand object to compare</param>
        /// <param name="right">The righthand object to compare</param>
        /// <returns>The <see cref="TypeCode"/> of the type that best suits comparing
        /// the two object.  It can be either <see cref="TypeCode.UInt64"/>,
        /// <see cref="TypeCode.Int64"/>, <see cref="TypeCode.Single"/>,
        /// <see cref="TypeCode.Double"/> or <see cref="TypeCode.Object"/></returns>
        public static TypeCode GetNumericType(object left, object right)
        {
            if (right == null || !(left is IConvertible) || !(right is IConvertible)) {
                return TypeCode.Object;
            }

            if (left is ulong || left is uint || left is ushort || left is byte) {
                return TypeCode.UInt64;
            } 
            
            if (left is long || left is int || left is short || left is sbyte) {
                return TypeCode.Int64;
            }

            if (left is float || right is float) {
                return TypeCode.Single;
            }

            if (left is double || left is decimal) {
                return TypeCode.Double;
            }

            return TypeCode.Object;
        }

        /// <summary>
        /// Tests for equality between two untyped objects, and if they are collections
        /// recursively checks for equality on each entry.
        /// </summary>
        /// <param name="left">The lefthand object to compare</param>
        /// <param name="right">The righthand object to compare</param>
        /// <returns>Whether or not the two objects are equal</returns>
        public static bool RecursiveEqual(this object left, object right)
        {
            switch (left) {
                case null:
                    return right == null;
                case string s:
                    return s.Equals(right as string, StringComparison.Ordinal);
                case IDictionaryObject dictObj:
                    return IsEqual(dictObj, right);
                case IArray arrayObj:
                    return IsEqual(arrayObj, right);
                case IList list:
                    return IsEqual(list, right);
                case IDictionary<string, object> dict:
                    return IsEqual(dict, right);
            }

            if (right == null) {
                return false;
            }
            
            switch (GetNumericType(left, right)) {
                case TypeCode.Single:
                {
                    return ((IConvertible) left).ToSingle(CultureInfo.InvariantCulture)
                         - ((IConvertible) right).ToSingle(CultureInfo.InvariantCulture) <= Single.Epsilon;
                }
                case TypeCode.Double:
                {
                    return ((IConvertible) left).ToDouble(CultureInfo.InvariantCulture)
                           - ((IConvertible) right).ToDouble(CultureInfo.InvariantCulture) <= Double.Epsilon;
                }
                case TypeCode.Int64:
                {
                    return ((IConvertible) left).ToInt64(CultureInfo.InvariantCulture)
                        .Equals(((IConvertible) right).ToInt64(CultureInfo.InvariantCulture));
                }
                case TypeCode.UInt64:
                {
                    return ((IConvertible) left).ToUInt64(CultureInfo.InvariantCulture)
                        .Equals(((IConvertible) right).ToUInt64(CultureInfo.InvariantCulture));
                }
            }

            return left.Equals(right);
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

        private static bool IsEqual(IDictionaryObject left, object right)
        {
            if (right == null || !(right is IDictionaryObject dict)) {
                return false;
            }

            if (left.Keys.Intersect(dict.Keys).Count() != left.Keys.Count) {
                return false;
            }

            return !(from key in left.Keys 
                let leftObj = left.GetValue(key) 
                let rightObj = dict.GetValue(key) 
                where !leftObj.RecursiveEqual(rightObj)
                select leftObj).Any();
        }

        private static bool IsEqual(IArray left, object right)
        {
            if (right == null || !(right is IArray arr)) {
                return false;
            }

            if (left.Count != arr.Count) {
                return false;
            }

            return !left.Where((t, i) => !t.RecursiveEqual(arr.GetValue(i))).Any();
        }

        private static bool IsEqual(IList left, object right)
        {
            if (right == null || !(right is IList list)) {
                return false;
            }

            if (left.Count != list.Count) {
                return false;
            }

            return !left.Cast<object>().Where((t, i) => !t.RecursiveEqual(list[i])).Any();
        }

        private static bool IsEqual(IDictionary<string, object> left, object right)
        {
            if (right == null || !(right is IDictionary<string, object> dict)) {
                return false;
            }

            if (left.Keys.Intersect(dict.Keys).Count() != left.Keys.Count) {
                return false;
            }

            return !(from key in left.Keys 
                let leftObj = left[key] 
                let rightObj = dict[key]
                where !leftObj.RecursiveEqual(rightObj)
                select leftObj).Any();
        }
    }
}

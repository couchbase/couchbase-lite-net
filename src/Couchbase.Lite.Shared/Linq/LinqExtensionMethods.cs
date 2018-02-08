//
//  LinqExtensionMethods.cs
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
#if CBL_LINQ
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Couchbase.Lite.Linq
{
    /// <summary>
    /// A collection of methods for use with the new LINQ model
    /// </summary>
    internal static class LinqExtensionMethods
    {
        #region Public Methods

        /// <summary>
        /// Returns whether or not the collection has at least one item AND at least one item
        /// matching the given pattern
        /// </summary>
        /// <typeparam name="T">The type of collection items to operate on</typeparam>
        /// <param name="collection">The collection to operate on (implicit)</param>
        /// <param name="predicate">The predicate to use</param>
        /// <returns><c>true</c> if the collection has at least one element and at least one element matching the 
        /// given pattern, <c>false</c> otherwise</returns>
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration", Justification = "Any only needs to check the first element")]
        public static bool AnyAndEvery<T>(this IEnumerable<T> collection, Func<T, bool> predicate)
        {
            return collection.Any() && collection.All(predicate);
        }

        /// <summary>
        /// Returns if a given <see cref="long" /> is between a given min and max
        /// </summary>
        /// <param name="l">The number to test (implicit)</param>
        /// <param name="min">The lower bound</param>
        /// <param name="max">The upper bound</param>
        /// <returns><c>true</c> if <c>l</c> is between <c>min</c> and <c>max</c>, <c>false</c> otherwise</returns>
        public static bool Between(this long l, long min, long max)
        {
            return l >= min && l <= max;
        }

        /// <summary>
        /// Returns if a given <see cref="int" /> is between a given min and max
        /// </summary>
        /// <param name="i">The number to test (implicit)</param>
        /// <param name="min">The lower bound</param>
        /// <param name="max">The upper bound</param>
        /// <returns><c>true</c> if <c>i</c> is between <c>min</c> and <c>max</c>, <c>false</c> otherwise</returns>
        public static bool Between(this int i, int min, int max)
        {
            return Between((long)i, min, max);
        }

        /// <summary>
        /// Returns if a given <see cref="short" /> is between a given min and max
        /// </summary>
        /// <param name="s">The number to test (implicit)</param>
        /// <param name="min">The lower bound</param>
        /// <param name="max">The upper bound</param>
        /// <returns><c>true</c> if <c>s</c> is between <c>min</c> and <c>max</c>, <c>false</c> otherwise</returns>
        public static bool Between(this short s, short min, short max)
        {
            return Between((long)s, min, max);
        }

        /// <summary>
        /// Returns if a given <see cref="sbyte" /> is between a given min and max
        /// </summary>
        /// <param name="b">The number to test (implicit)</param>
        /// <param name="min">The lower bound</param>
        /// <param name="max">The upper bound</param>
        /// <returns><c>true</c> if <c>b</c> is between <c>min</c> and <c>max</c>, <c>false</c> otherwise</returns>
        public static bool Between(this sbyte b, sbyte min, sbyte max)
        {
            return Between((long)b, min, max);
        }

        /// <summary>
        /// Returns if a given <see cref="ulong" /> is between a given min and max
        /// </summary>
        /// <param name="u">The number to test (implicit)</param>
        /// <param name="min">The lower bound</param>
        /// <param name="max">The upper bound</param>
        /// <returns><c>true</c> if <c>u</c> is between <c>min</c> and <c>max</c>, <c>false</c> otherwise</returns>
        public static bool Between(this ulong u, ulong min, ulong max)
        {
            return u >= min && u <= max;
        }

        /// <summary>
        /// Returns if a given <see cref="uint" /> is between a given min and max
        /// </summary>
        /// <param name="u">The number to test (implicit)</param>
        /// <param name="min">The lower bound</param>
        /// <param name="max">The upper bound</param>
        /// <returns><c>true</c> if <c>u</c> is between <c>min</c> and <c>max</c>, <c>false</c> otherwise</returns>
        public static bool Between(this uint u, uint min, uint max)
        {
            return Between((ulong)u, min, max);
        }

        /// <summary>
        /// Returns if a given <see cref="ushort" /> is between a given min and max
        /// </summary>
        /// <param name="u">The number to test (implicit)</param>
        /// <param name="min">The lower bound</param>
        /// <param name="max">The upper bound</param>
        /// <returns><c>true</c> if <c>u</c> is between <c>min</c> and <c>max</c>, <c>false</c> otherwise</returns>
        public static bool Between(this ushort u, ushort min, ushort max)
        {
            return Between((ulong)u, min, max);
        }

        /// <summary>
        /// Returns if a given <see cref="byte" /> is between a given min and max
        /// </summary>
        /// <param name="b">The number to test (implicit)</param>
        /// <param name="min">The lower bound</param>
        /// <param name="max">The upper bound</param>
        /// <returns><c>true</c> if <c>u</c> is between <c>min</c> and <c>max</c>, <c>false</c> otherwise</returns>
        public static bool Between(this byte b, byte min, byte max)
        {
            return Between((ulong)b, min, max);
        }

        public static bool Like(this string item, string pattern)
        {
            return false;
        }

        public static string Id(this object obj)
        {
            return String.Empty;
        }

        public static ulong Sequence(this object obj)
        {
            return 0UL;
        }

        #endregion
    }
}
#endif
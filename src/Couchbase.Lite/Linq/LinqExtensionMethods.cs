//
//  LinqExtensionMethods.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Couchbase.Lite.Linq
{
    public static class LinqExtensionMethods
    {
        public static bool AnyAndEvery<T>(this IEnumerable<T> collection, Func<T, bool> predicate)
        {
            return collection.Any() && collection.All(x => predicate(x));
        }

        public static bool IsRegexMatch(this string item, string pattern)
        {
            var regex = new Regex(pattern);
            return regex.IsMatch(item);
        }

        public static bool Between(this long l, long min, long max)
        {
            return l >= min && l <= max;
        }

        public static bool Between(this int i, int min, int max)
        {
            return Between((long)i, min, max);
        }

        public static bool Between(this short s, short min, short max)
        {
            return Between((long)s, min, max);
        }

        public static bool Between(this sbyte b, sbyte min, sbyte max)
        {
            return Between((long)b, min, max);
        }

        public static bool Between(this ulong u, ulong min, ulong max)
        {
            return u >= min && u <= max;
        }

        public static bool Between(this uint u, uint min, uint max)
        {
            return Between((ulong)u, min, max);
        }

        public static bool Between(this ushort u, ushort min, ushort max)
        {
            return Between((ulong)u, min, max);
        }

        public static bool Between(this byte b, byte min, byte max)
        {
            return Between((ulong)b, min, max);
        }
    }
}

//
// Util.cs
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
using System.Linq;

namespace Couchbase.Lite.Storage.Internal
{
    internal static class Utility
    {
        internal static T? GetNullable<T>(this IDictionary<string, object> collection, string key) where T : struct
        {
            object value = collection.Get(key);
            return ExtensionMethods.CastOrDefault<T>(value);
        }

        internal static string JoinQuoted(IEnumerable<string> strings)
        {
            if (!strings.Any()) {
                return String.Empty;
            }

            var result = "'";
            var first = true;

            foreach (string str in strings)
            {
                if (first)
                    first = false;
                else
                    result = result + "','";

                result = result + Database.Quote(str);
            }

            result = result + "'";

            return result;
        }
    }
}


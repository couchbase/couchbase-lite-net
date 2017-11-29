// 
// Collation.cs
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

using System.Globalization;
using Couchbase.Lite.Internal.Query;

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A factory class for creating <see cref="ICollation"/> instances
    /// </summary>
    public static class Collation
    {
        /// <summary>
        /// The default locale for the current program, for use with Unicode collation
        /// </summary>
		public static readonly string DefaultLocale = CultureInfo.CurrentCulture.Name == "" ?
																 "en" : CultureInfo.CurrentCulture.Name.Replace('-', '_');
        /// <summary>
        /// Creates an ASCII based collation instance
        /// </summary>
        /// <returns>An ASCII based collation instance</returns>
        [NotNull]
        public static IASCIICollation ASCII()
        {
            return new QueryCollation(false);
        }

        /// <summary>
        /// Creates a Unicode based collation instance (http://unicode.org/reports/tr10/)
        /// </summary>
        /// <returns>A Unicode based collation instance</returns>
        [NotNull]
        public static IUnicodeCollation Unicode()
        {
            return new QueryCollation(true);
        }
    }
}
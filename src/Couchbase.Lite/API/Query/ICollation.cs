// 
// ICollation.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A base interface for different collations
    /// </summary>
    public interface ICollation
    {
        
    }

    /// <summary>
    /// An interface that can use 7-bit ASCII rules to do string collation
    /// </summary>
    public interface IASCIICollation : ICollation
    {
        /// <summary>
        /// Instructs the object to either ignore lowercase vs uppercase when collating
        /// or consider it (default is to consider)
        /// </summary>
        /// <param name="ignoreCase">Whether or not to ignore casing</param>
        /// <returns>The collation object for further processing</returns>
        [NotNull]
        IASCIICollation IgnoreCase(bool ignoreCase);
    }

    /// <summary>
    /// An interface that can use Unicode rules (http://unicode.org/reports/tr10/)
    /// to do string collation
    /// </summary>
    public interface IUnicodeCollation : ICollation
    {
        /// <summary>
        /// Instructs the object to either ignore lowercase vs uppercase when collating
        /// or consider it (default is to consider)
        /// </summary>
        /// <param name="ignoreCase">Whether or not to ignore casing</param>
        /// <returns>The collation object for further processing</returns>
        [NotNull]
        IUnicodeCollation IgnoreCase(bool ignoreCase);

        /// <summary>
        /// Instructs the object to either diacritics (e.g. accents) when collating
        /// or consider it (default is to consider)
        /// </summary>
        /// <param name="ignoreAccents">Whether or not to ignore diacritics</param>
        /// <returns>The collation object for further processing</returns>
        [NotNull]
        IUnicodeCollation IgnoreAccents(bool ignoreAccents);

        /// <summary>
        /// Sets the locale to use when applying the collation rules
        /// </summary>
        /// <param name="locale">The POSIX locale code (ISO-639 language code 
        /// plus an optional underbar [_] and ISO-3166 country code.  Example: 
        /// 'en', 'en_US', 'fr_CA', etc)</param>
        /// <returns>The collation object for further processing</returns>
        [NotNull]
        IUnicodeCollation Locale(string locale);
    }
}
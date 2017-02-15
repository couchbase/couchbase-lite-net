//
//  IndexOptions.cs
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

using LiteCore.Interop;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing options for creating an index in a database
    /// </summary>
    public sealed class IndexOptions
    {
        #region Properties

        /// <summary>
        /// Gets or sets whether or not to ignore diacriticals
        /// (i.e. accent marks, etc) when full text searching
        /// </summary>
        public bool IgnoreDiacriticals { get; set; }

        /// <summary>
        /// Gets or sets the language to use for full text search
        /// </summary>
        public string Language { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public IndexOptions()
        {

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="language">The language to use for full text search</param>
        /// <param name="ignoreDiacriticals">Whether or not to ignore diacriticals
        /// (i.e. accent marks, etc) when full text searching</param>
        public IndexOptions(string language, bool ignoreDiacriticals)
        {
            Language = language;
            IgnoreDiacriticals = ignoreDiacriticals;
        }

        #endregion

        #region Internal Methods

        internal static C4IndexOptions Internal(IndexOptions options)
        {
            return new C4IndexOptions {
                language = options.Language,
                ignoreDiacritics = options.IgnoreDiacriticals
            };
        }

        #endregion
    }
}

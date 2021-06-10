// 
// FullTextIndexConfiguration.cs
// 
// Copyright (c) 2021 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Internal.Query;
using LiteCore.Interop;
using System.Globalization;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An class for an index based on full text searching
    /// </summary>
    public sealed class FullTextIndexConfiguration : IndexConfiguration
    {
        #region Variables

        private bool _ignoreAccents;
        private string _locale = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether or not to ignore accents when performing 
        /// the full text search
        /// </summary>
        public bool IgnoreAccents => _ignoreAccents;

        /// <summary>
        /// Gets the locale to use when performing full text searching
        /// </summary>
        public string Language => _locale;

        internal override C4IndexOptions Options => new C4IndexOptions {
            ignoreDiacritics = _ignoreAccents,
            language = _locale
        };
        #endregion

        #region Constructors

        /// <summary>
        /// Starts the creation of an index based on a full text search
        /// </summary>
        /// <param name="expressions">The expressions to use to create the index</param>
        /// <param name="ignoreAccents">The boolean value to ignore accents when performing the full text search</param>
        /// <param name="locale">The locale to use when performing full text searching</param>
        /// <returns>The beginning of an FTS based index</returns>
        public FullTextIndexConfiguration(string[] expressions, bool ignoreAccents = false, 
            string locale = null)
            : base(C4IndexType.FullTextIndex, expressions)
        {
            _ignoreAccents = ignoreAccents;
            if (!string.IsNullOrEmpty(locale)) {
                _locale = locale;
            }
        }

        /// <summary>
        /// Starts the creation of an index based on a full text search
        /// </summary>
        /// <param name="expressions">The expressions to use to create the index</param>
        /// <returns>The beginning of an FTS based index</returns>
        public FullTextIndexConfiguration(params string[] expressions)
            : base(C4IndexType.FullTextIndex, expressions)
        {
        }

        #endregion
    }
}

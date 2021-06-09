// 
// IndexDescriptor.cs
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

using LiteCore.Interop;
using System.Globalization;

namespace Couchbase.Lite.Internal.Query
{
    internal class IndexDescriptor
    {
        #region Variables

        private bool _ignoreAccents;
        private string _locale = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        #endregion

        #region Properties

        protected C4QueryLanguage QueryLanguage { get; }

        internal C4IndexType IndexType { get; }

        internal C4IndexOptions Options
        {
            get
            {
                if (IndexType == C4IndexType.FullTextIndex) {
                    return new C4IndexOptions
                    {
                        ignoreDiacritics = _ignoreAccents,
                        language = _locale
                    };
                }

                return new C4IndexOptions();
            }
        }

        #endregion

        #region Constructor

        protected IndexDescriptor(C4IndexType indexType, C4QueryLanguage queryLanguage)
        {
            IndexType = indexType;
            QueryLanguage = queryLanguage;
        }

        #endregion

        #region Public Methods

        public IndexDescriptor IgnoreAccents(bool ignoreAccents)
        {
            _ignoreAccents = ignoreAccents;
            return this;
        }

        public IndexDescriptor SetLanguage(string language)
        {
            _locale = language;
            return this;
        }

        #endregion
    }
}

// 
// IndexConfiguration.cs
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

using Couchbase.Lite.Query;
using JetBrains.Annotations;
using LiteCore.Interop;
using System;

namespace Couchbase.Lite.Internal.Query
{
    internal class IndexConfigurationBase : IndexDescriptor, IValueIndexConfiguration, IFullTextIndexConfiguration
    {
        [CanBeNull]
        private string[] _expression;

        protected IndexConfigurationBase(C4IndexType indexType, params string[] items)
            :base(indexType, C4QueryLanguage.N1QLQuery)
        {
            _expression = items;
        }

        #region Internal Methods

        internal string ToN1QL()
        {
            if (_expression.Length == 1)
                return _expression[0];

            return String.Join(",", _expression);
        }

        #endregion

        #region IFullTextIndexConfiguration

        public new IFullTextIndexConfiguration IgnoreAccents(bool ignoreAccents)
        {
            base.IgnoreAccents(ignoreAccents);
            return this;
        }

        public new IFullTextIndexConfiguration SetLanguage(string language)
        {
            base.SetLanguage(language);
            return this;
        }

        #endregion
    }

    /// <summary>
    /// An class for an index based on a simple property value
    /// </summary>
    internal class ValueIndexConfiguration : IndexConfigurationBase
    {
        public ValueIndexConfiguration(params string[] expressions)
            : base(C4IndexType.ValueIndex, expressions)
        {
        }
    }

    /// <summary>
    /// An class for an index based on full text searching
    /// </summary>
    internal class FullTextIndexConfiguration : IndexConfigurationBase
    {
        public FullTextIndexConfiguration(params string[] expressions)
            : base(C4IndexType.FullTextIndex, expressions)
        {
        }
    }
}

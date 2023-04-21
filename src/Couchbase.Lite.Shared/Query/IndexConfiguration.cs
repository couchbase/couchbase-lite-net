﻿// 
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

using LiteCore.Interop;
using System;
using System.Diagnostics;

namespace Couchbase.Lite.Internal.Query
{
    public abstract class IndexConfiguration
    {
        #region Properties

        /// <summary>
        /// Gets the expressions to use to create the index
        /// </summary>
        public string[]? Expressions { get; }

        internal C4QueryLanguage QueryLanguage { get; }

        internal C4IndexType IndexType { get; }

        internal abstract C4IndexOptions Options { get; }

        #endregion

        #region Constructor

        internal IndexConfiguration(C4IndexType indexType, params string[] items)
            : this(indexType, C4QueryLanguage.N1QLQuery)
        {
            Expressions = items;
        }

        internal IndexConfiguration(C4IndexType indexType, C4QueryLanguage queryLanguage)
        {
            IndexType = indexType;
            QueryLanguage = queryLanguage;
        }

        #endregion

        #region Internal Methods

        internal string ToN1QL()
        {
            Debug.Assert(Expressions != null);
            if (Expressions.Length == 1)
                return Expressions[0];

            return String.Join(",", Expressions);
        }

        #endregion
    }
}

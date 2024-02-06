﻿// 
// ValueIndexConfiguration.cs
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

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An class for an index based on a simple property value
    /// </summary>
    public sealed class ValueIndexConfiguration : IndexConfiguration
    {
        #region Properties
        internal override C4IndexOptions Options => new C4IndexOptions();
        #endregion

        #region Constructors

        /// <summary>
        /// Starts the creation of an index based on a simple property
        /// </summary>
        /// <param name="expressions">The expressions to use to create the index</param>
        /// <returns>The beginning of a value based index</returns>
        public ValueIndexConfiguration(params string[] expressions)
            : base(C4IndexType.ValueIndex, expressions)
        {
        }
        #endregion
    }
}

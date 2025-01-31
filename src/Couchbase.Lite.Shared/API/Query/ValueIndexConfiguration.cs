// 
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
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An class for an index based on one or more simple property values
    /// </summary>
    public sealed class ValueIndexConfiguration : IndexConfiguration
    {
        internal override C4IndexOptions Options => new C4IndexOptions
        {
            where = Where
        };

        /// <summary>
        /// A predicate expression defining conditions for indexing documents. 
        /// Only documents satisfying the predicate are included, enabling partial indexes.
        /// </summary>
        public string? Where { get; set; }

        /// <summary>
        /// Starts the creation of an index based on one or more simple property values
        /// </summary>
        /// <param name="expressions">The expressions to use to create the index</param>
        public ValueIndexConfiguration(params string[] expressions)
            : base(C4IndexType.ValueIndex, expressions)
        {
        }

        /// <summary>
        /// Starts the creation of an index based on one or more simple property values,
        /// and a predicate for enabling partial indexes.
        /// </summary>
        /// <param name="expressions">The expressions to use to create the index</param>
        /// <param name="where">A where clause used to determine whether or not to include a particular doc</param>
        public ValueIndexConfiguration(IEnumerable<string> expressions, string? where = null)
            : base(C4IndexType.ValueIndex, expressions.ToArray())
        {
            Where = where;
        }
    }
}

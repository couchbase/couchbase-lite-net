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

using LiteCore.Interop;
using System;
using System.Diagnostics;
using System.Linq;

namespace Couchbase.Lite.Internal.Query
{
    /// <summary>
    /// A configuration object that stores the details of how a given <see cref="Couchbase.Lite.Query.IIndex"/>
    /// object should be created
    /// </summary>
    public abstract class IndexConfiguration
    {
        /// <summary>
        /// Gets the expressions to use to create the index
        /// </summary>
        public string[]? Expressions { get; }

        internal C4IndexType IndexType { get; }

        internal abstract C4IndexOptions Options { get; }

        internal IndexConfiguration(C4IndexType indexType, string[]? items)
        {
            if(items != null) {
                if (items.Length == 0) {
                    throw new ArgumentException("Empty list of expressions not allowed");
                }

                if (items.Any(String.IsNullOrEmpty)) {
                    throw new ArgumentException("Empty / null strings not allowed in list of expressions");
                }
            }
            


            IndexType = indexType;
            Expressions = items;
        }

        internal virtual void Validate()
        {

        }

        internal string? ToN1QL()
        {
            if(Expressions == null) {
                return null;
            }

            return String.Join(",", Expressions);
        }
    }
}

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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;
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
        private const string Tag = nameof(IndexConfiguration);

        /// <summary>
        /// Gets the expressions to use to create the index
        /// </summary>
        public string[]? Expressions { get; }

        internal C4IndexType IndexType { get; }

        internal abstract C4IndexOptions Options { get; }

        internal IndexConfiguration(C4IndexType indexType, string[]? expressions)
        {
            if(expressions != null) {
                // Quick sanity check.  If I allow this to proceed, no error will happen but
                // perhaps unintended behavior will.  The final string to pass will be empty
                // and LiteCore will index the entire path element.  This is ok in some cases
                // like ArrayIndexConfiguration, but it should be manifest with a null array
                // and not an empty array or array of empty or array of null.
                if(!expressions.Any(x => !String.IsNullOrEmpty(x))) {
                    throw new ArgumentException("Only strings that are non-null and non-empty are allowed", "expressions");
                }
            }

            // Expressions may still be invalid here, but the user will be alerted when they
            // try to create an index when LiteCore rejects it.
            IndexType = indexType;
            Expressions = expressions;
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

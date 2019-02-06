﻿// 
// Ordering.cs
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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Util;
using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A factory class for generating <see cref="ISortOrder"/> objects
    /// </summary>
    public static class Ordering
    {
        #region Constants

        private const string Tag = nameof(Ordering);

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates an object that will sort based on the given expression
        /// </summary>
        /// <param name="expression">The expression to use when sorting</param>
        /// <returns>The object that will perform the sort</returns>
        [NotNull]
        public static ISortOrder Expression([NotNull]IExpression expression) => 
            new SortOrder(CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        /// <summary>
        /// Creates an object that will sort based on the value in the given
        /// property path
        /// </summary>
        /// <param name="property">The path of the property whose value will be used
        /// to sort the results of the query</param>
        /// <returns>The object that will perform the sort</returns>
        [NotNull]
        public static ISortOrder Property([NotNull]string property) => Expression(Lite.Query.Expression.Property(property));

        #endregion
    }
}

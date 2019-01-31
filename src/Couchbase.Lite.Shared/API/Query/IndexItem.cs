// 
// IndexItem.cs
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
    /// A factory class for creating <see cref="IValueIndexItem"/> instances
    /// </summary>
    public static class ValueIndexItem
    {
        #region Constants

        private const string Tag = nameof(ValueIndexItem);

        #endregion

        /// <summary>
        /// Creates a value index item based on a given property path
        /// </summary>
        /// <param name="property">The property path to base the index item on</param>
        /// <returns>The created index item</returns>
        [NotNull]
        public static IValueIndexItem Property([NotNull]string property) =>
            Expression(Lite.Query.Expression.Property(property));

        /// <summary>
        /// Creates a value index item based on a given <see cref="IExpression"/>
        /// </summary>
        /// <param name="expression">The expression to base the index item on</param>
        /// <returns>The created index item</returns>
        [NotNull]
        public static IValueIndexItem Expression([NotNull]IExpression expression) => 
            new QueryIndexItem(CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));
    }

    /// <summary>
    /// A factory class for creating <see cref="IFullTextIndexItem"/> instances
    /// </summary>
    public static class FullTextIndexItem
    {
        /// <summary>
        /// Creates an FTS index item based on a given <see cref="IExpression"/>
        /// </summary>
        /// <param name="property">The property name to base the index item on</param>
        /// <returns>The created index item</returns>
        [NotNull]
        public static IFullTextIndexItem Property([NotNull]string property) => new QueryIndexItem(Expression.Property(property));
    }
}
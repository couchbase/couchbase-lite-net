// 
// IndexBuilder.cs
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

using Couchbase.Lite.Internal.Query;

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A factory class for creating <see cref="IIndex"/> instances
    /// </summary>
    public static class IndexBuilder
    {
        /// <summary>
        /// Starts the creation of an index based on a simple property
        /// </summary>
        /// <param name="items">The items to use to create the index</param>
        /// <returns>The beginning of a value based index</returns>
        [NotNull]
        public static IValueIndex ValueIndex(params IValueIndexItem[] items) => new QueryIndex(items);

        /// <summary>
        /// Starts the creation of an index based on a full text search
        /// </summary>
        /// <param name="items">The items to use to create the index</param>
        /// <returns>The beginning of an FTS based index</returns>
        [NotNull]
        public static IFullTextIndex FullTextIndex(params IFullTextIndexItem[] items) => new QueryIndex(items);
    }
}
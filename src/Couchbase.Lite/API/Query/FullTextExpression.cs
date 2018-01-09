// 
//  FullTextExpression.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using System;

using Couchbase.Lite.Internal.Query;

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A class that generates expressions that operate on the results of full-text searching
    /// </summary>
    public static class FullTextExpression
    {
        #region Public Methods

        /// <summary>
        /// Generates a query expression that will check for matches against a
        /// given full text index name
        /// </summary>
        /// <param name="name">The name of the full-text index to perform the
        /// check against</param>
        /// <returns>The generated query expression</returns>
        [NotNull]
        public static IFullTextExpression Index(string name) => new QueryCompoundExpression("MATCH", name, String.Empty);

        #endregion
    }
}
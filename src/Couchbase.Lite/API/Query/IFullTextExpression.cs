// 
//  IFullTextExpression.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An interface that represents an expression that is eligible to receive
    /// full-text related query clauses
    /// </summary>
    public interface IFullTextExpression
    {
        /// <summary>
        /// Returns an expression that will evaluate whether or not the given
        /// expression full text matches the current one
        /// </summary>
        /// <param name="text">The text to use for the match operation</param>
        /// <returns>The expression representing the new operation</returns>
        [NotNull]
        IExpression Match(string text);
    }
}
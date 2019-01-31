// 
//  IArrayExpression.cs
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
    /// An interface that represents a portion of a query that chooses
    /// a collection to be used in a query of each of its elements
    /// </summary>
    public interface IArrayExpressionIn
    {
        /// <summary>
        /// Chooses a collection to be used in a query of each of
        /// its elements
        /// </summary>
        /// <param name="expression">An expression that evaluates to a collection type</param>
        /// <returns>An object that will determine the predicate for the contents
        /// of the collection</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        IArrayExpressionSatisfies In(IExpression expression);
    }

    /// <summary>
    /// An interface representing an object that can accept a predicate to use
    /// on each item in a collection
    /// </summary>
    public interface IArrayExpressionSatisfies
    {
        /// <summary>
        /// Accepts a predicate to apply to each item of a collection
        /// received from <see cref="IArrayExpressionIn"/>
        /// </summary>
        /// <param name="expression">The predicate expression to apply</param>
        /// <returns>The overall expression for further processing</returns>
        [NotNull]
        [ContractAnnotation("null => halt")]
        IExpression Satisfies(IExpression expression);
    }
}
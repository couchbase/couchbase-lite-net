// 
// IMetaExpression.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// Represents an expression that is meant to retrieve metadata information
    /// inside of an <see cref="IQuery"/>
    /// </summary>
    public interface IMetaExpression : IExpression
    {
        #region Public Methods

        /// <summary>
        /// Specifies the source to retrieve the information from
        /// if multiple sources are used in a query
        /// </summary>
        /// <param name="alias">The name of the data source</param>
        /// <returns>The expression with the alias added</returns>
        [NotNull]
        IExpression From([NotNull]string alias);

        #endregion
    }
}
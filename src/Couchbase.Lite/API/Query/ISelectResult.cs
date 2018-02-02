// 
// ISelectResult.cs
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
    /// An interface that holds information about what to
    /// select out of an <see cref="IQuery"/>
    /// </summary>
    public interface ISelectResult
    {}

    /// <summary>
    /// An interface representing a select result "FROM" a certain data
    /// source
    /// </summary>
    public interface ISelectResultFrom : ISelectResult
    {
        /// <summary>
        /// Specifies the source of this select result
        /// </summary>
        /// <param name="alias">The alias of the data source to select from</param>
        /// <returns>The modified select result</returns>
        [NotNull]
        ISelectResult From(string alias);
    }

    /// <summary>
    /// An interface reprsenting a select result that can be aliased to
    /// an arbitrary name
    /// </summary>
    public interface ISelectResultAs : ISelectResult
    {
        /// <summary>
        /// Adds an alias to the select result
        /// </summary>
        /// <param name="alias">The alias to assign to the select result</param>
        /// <returns>The modified select result</returns>
        [NotNull]
        ISelectResult As(string alias);
    }
}

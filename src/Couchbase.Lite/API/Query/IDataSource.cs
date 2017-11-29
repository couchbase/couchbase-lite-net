// 
// IDataSource.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
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
    /// An interface representing the source of data for a query
    /// </summary>
    public interface IDataSource
    {
        
    }

    /// <summary>
    /// An interface representing a source of data that comes from
    /// an <see cref="Database"/>
    /// </summary>
    public interface IDataSourceAs : IDataSource
    {
        #region Public Methods

        /// <summary>
        /// Attaches an alias to a given data source
        /// </summary>
        /// <param name="alias">The alias to attach</param>
        /// <returns>The datasource, for further operations</returns>
        [NotNull]
        IDataSource As(string alias);

        #endregion
    }
}

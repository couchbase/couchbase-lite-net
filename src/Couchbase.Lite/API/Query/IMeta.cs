// 
// IMeta.cs
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

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An interface representing an object that can generate expressions
    /// for retrieving metadata information during an <see cref="IQuery"/>
    /// </summary>
    public interface IMeta
    {
        #region Properties

        /// <summary>
        /// Gets an expression for retrieving the unique ID of a document
        /// </summary>
        IMetaExpression ID { get; }

        /// <summary>
        /// Gets an expression for retrieving the sequence of a document
        /// (i.e. the auto-incrementing entry in the database)
        /// </summary>
        IMetaExpression Sequence { get; }

        #endregion
    }
}

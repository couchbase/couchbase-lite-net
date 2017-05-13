// 
// IReadOnlyObjectFragment.cs
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

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface representing a readonly entry in a key path that is
    /// able to be indexed by <see cref="System.String"/>
    /// (e.g. object["key1"]["key2"])
    /// </summary>
    public interface IReadOnlyDictionaryFragment
    {
        #region Properties

        /// <summary>
        /// Gets the value of the given key, or lack thereof,
        /// wrapped inside of a <see cref="ReadOnlyFragment"/>
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value of the given key, or lack thereof</returns>
        ReadOnlyFragment this[string key] { get; }

        #endregion
    }

    /// <summary>
    /// An interface representing a readonly entry in a key path
    /// that is able to be indexed by position
    /// (e.g. object[0][1])
    /// </summary>
    public interface IReadOnlyArrayFragment
    {
        #region Properties

        /// <summary>
        /// Gets the value of the given index, or lack thereof,
        /// wrapped inside of a <see cref="ReadOnlyFragment"/>
        /// </summary>
        /// <param name="index">The index to check</param>
        /// <returns>The value of the given index, or lack thereof</returns>
        ReadOnlyFragment this[int index] { get; }

        #endregion
    }
}

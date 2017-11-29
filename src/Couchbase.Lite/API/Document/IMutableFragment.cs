// 
// IFragment.cs
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

using System;

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface representing a writeable object capable of being indexed 
    /// via <see cref="System.String"/>
    /// </summary>
    public interface IMutableDictionaryFragment
    {
        #region Properties

        /// <summary>
        /// Gets the value of an arbitrary <see cref="System.String"/> key
        /// </summary>
        /// <param name="key">The key to lookup the value for</param>
        /// <returns>The value, or lack thereof, wrapped in a <see cref="IMutableFragment"/></returns>
        [NotNull]
        IMutableFragment this[string key] { get; }

        #endregion
    }

    /// <summary>
    /// An interface representing a writeable object capable of being indexed
    /// via <see cref="System.Int32"/>
    /// </summary>
    public interface IMutableArrayFragment
    {
        #region Properties

        /// <summary>
        /// Gets the value of an arbitrary index
        /// </summary>
        /// <param name="index">The index to lookup the value for</param>
        /// <returns>The value, or lack thereof, wrapped in a <see cref="IMutableFragment"/></returns>
        [NotNull]
        IMutableFragment this[int index] { get; }

        #endregion
    }

    public interface IMutableFragment : IMutableArrayFragment, IMutableDictionaryFragment
    {
        [CanBeNull]
        object Value { get; set; }
        
        [CanBeNull]
        MutableArray Array { get; set; }
        
        [CanBeNull]
        Blob Blob { get; set; }

        bool Boolean { get; set; }
        
        DateTimeOffset Date { get; set; }
        
        [CanBeNull]
        MutableDictionary Dictionary { get; set; }

        double Double { get; set; }

        float Float { get; set; }

        int Int { get; set; }

        long Long { get; set; }
        
        [CanBeNull]
        string String { get; set; }
    }
}

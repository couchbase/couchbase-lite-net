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

using System;

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface representing a readonly entry in a key path that is
    /// able to be indexed by <see cref="String"/>
    /// (e.g. object["key1"]["key2"])
    /// </summary>
    public interface IDictionaryFragment
    {
        #region Properties

        /// <summary>
        /// Gets the value of the given key, or lack thereof,
        /// wrapped inside of a <see cref="IFragment"/>
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>The value of the given key, or lack thereof</returns>
        [NotNull]
        IFragment this[string key] { get; }

        #endregion
    }

    /// <summary>
    /// An interface representing a readonly entry in a key path
    /// that is able to be indexed by position
    /// (e.g. object[0][1])
    /// </summary>
    public interface IArrayFragment
    {
        #region Properties

        /// <summary>
        /// Gets the value of the given index, or lack thereof,
        /// wrapped inside of a <see cref="IFragment"/>
        /// </summary>
        /// <param name="index">The index to check</param>
        /// <returns>The value of the given index, or lack thereof</returns>
        [NotNull]
        IFragment this[int index] { get; }

        #endregion
    }

    /// <summary>
    /// An interface representing a readonly entry in a key-value path on
    /// an object.  
    /// </summary>
    public interface IFragment : IArrayFragment, IDictionaryFragment
    {
        /// <summary>
        /// Gets whether or not this object exists in the hierarchy
        /// </summary>
        bool Exists { get; }

        /// <summary>
        /// Gets the value of the fragment as an untyped object
        /// </summary>
        [CanBeNull]
        object Value { get; }

        /// <summary>
        /// Gets the contained value as a <see cref="ArrayObject"/>
        /// </summary>
        [CanBeNull]
        ArrayObject Array { get; }

        /// <summary>
        /// Gets the contained value as a <see cref="Blob"/>
        /// </summary>
        [CanBeNull]
        Blob Blob { get; }

        /// <summary>
        /// Gets the contained value as a <see cref="Boolean" />
        /// </summary>
        /// <remarks>The following process is used for evaluation:
        /// 
        /// <see cref="Boolean"/> values are evaluated as is
        /// <c>null</c> is <c>false</c>
        /// Non-zero number values are <c>true</c>
        /// Everything else is <c>true</c>
        /// </remarks>
        bool Boolean { get; }
        
        /// <summary>
        /// Gets the contained value as a <see cref="DateTimeOffset"/>
        /// </summary>
        DateTimeOffset Date { get; }

        /// <summary>
        /// Gets the contained value as a <see cref="DictionaryObject"/>
        /// </summary>
        [CanBeNull]
        DictionaryObject Dictionary { get; }

        /// <summary>
        /// Gets the contained value as a <see cref="Double"/>
        /// </summary>
        /// <remarks><c>true</c> will be converted to 1.0, and everything else that
        /// is non-numeric will be 0.0</remarks>
        double Double { get; }

        /// <summary>
        /// Gets the contained value as a <see cref="Single"/>
        /// </summary>
        /// <remarks><c>true</c> will be converted to 1.0f, and everything else that
        /// is non-numeric will be 0.0f</remarks>
        float Float { get; }

        /// <summary>
        /// Gets the contained value as an <see cref="Int32"/>
        /// </summary>
        /// <remarks><c>true</c> will be converted to 1, a <see cref="Double"/> value
        /// will be rounded, and everything else non-numeric will be 0</remarks>
        int Int { get; }

        /// <summary>
        /// Gets the contained value as an <see cref="Int64"/>
        /// </summary>
        /// <remarks><c>true</c> will be converted to 1, a <see cref="Double"/> value
        /// will be rounded, and everything else non-numeric will be 0</remarks>
        long Long { get; }
        
        /// <summary>
        /// Gets the contained value as a <see cref="String"/>
        /// </summary>
        [CanBeNull]
        string String { get; }
    }
}

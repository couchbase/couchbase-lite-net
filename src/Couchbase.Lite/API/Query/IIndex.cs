// 
// IIndex.cs
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
    /// The base interface for an index in a <see cref="Database"/>
    /// </summary>
    public interface IIndex
    {
        
    }

    /// <summary>
    /// An interface for an index based on a simple property value
    /// </summary>
    public interface IValueIndex : IIndex
    {
        
    }

    /// <summary>
    /// An interface for an index based on full text searching
    /// </summary>
    public interface IFTSIndex : IIndex
    {
        /// <summary>
        /// Sets whether or not to ignore accents when performing 
        /// the full text search
        /// </summary>
        /// <param name="ignoreAccents">Whether or not to ignore accents</param>
        /// <returns>The index for further processing</returns>
        [NotNull]
        IFTSIndex IgnoreAccents(bool ignoreAccents);

        /// <summary>
        /// Sets the locale to use when performing full text searching
        /// </summary>
        /// <param name="localeCode">The locale code in the form of ISO-639 language code plus, optionally, 
        /// an underscore and an ISO-3166 country code: "en", "en_US", "fr_CA", etc.</param>
        /// <returns>The index for further processing</returns>
        [NotNull]
        IFTSIndex SetLocale(string localeCode);
    }
}
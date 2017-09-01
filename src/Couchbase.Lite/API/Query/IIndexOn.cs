// 
// IIndexOn.cs
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
    /// An interface for use when creating value based indexes (e.g.
    /// Index.ValueIndex().On(...)
    /// </summary>
    public interface IValueIndexOn
    { 
        /// <summary>
        /// Specifies the items to create a value based index on
        /// </summary>
        /// <param name="items">The items to create the index on</param>
        /// <returns>The index object</returns>
        IValueIndex On(params IValueIndexItem[] items);
    }

    /// <summary>
    /// An interface for use when creating FTS based indexes (e.g.
    /// Index.FTSIndex().On(...)
    /// </summary>
    public interface IFTSIndexOn
    {
        /// <summary>
        /// Specifies the items to create an FTS based index on
        /// </summary>
        /// <param name="items">The items to create the index on</param>
        /// <returns>The index object</returns>
        IFTSIndex On(params IFTSIndexItem[] items);
    }
}
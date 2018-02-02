// 
//  IResultSet.cs
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

using System;
using System.Collections.Generic;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An interface representing an enumerable collection of results
    /// from a given <see cref="IQuery"/>.
    /// </summary>
    /// <warning>
    /// Multiple enumerations are not supported.  If you wish to enumerate
    /// more than once, then use <see cref="AllResults"/> or another LINQ
    /// method to materialize the results.
    /// </warning>
    public interface IResultSet : IEnumerable<Result>
    {
        /// <summary>
        /// Cross platform API entry to get all results in a list.  Same
        /// as <c>ToList()</c>
        /// </summary>
        /// <returns>A list of results from the result set</returns>
        List<Result> AllResults();
    }
}

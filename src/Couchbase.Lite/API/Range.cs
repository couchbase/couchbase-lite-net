//
//  Range.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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

namespace Couchbase.Lite
{
    /// <summary>
    /// A struct representing an arbitrary range
    /// </summary>
    public struct Range
    {
        /// <summary>
        /// Gets the start position of the range
        /// </summary>
        public uint Start { get; }

        /// <summary>
        /// Gets the length of the range
        /// </summary>
        public uint Length { get; }

        /// <summary>
        /// Construtor
        /// </summary>
        /// <param name="start">The start of the range</param>
        /// <param name="length">The length of the range</param>
        public Range(uint start, uint length)
        {
            Start = start;
            Length = length;
        }
    }
}

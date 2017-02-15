//
//  IFullTextQueryRow.cs
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
    /// An interface describing an entry in the result of a
    /// full text query
    /// </summary>
    public interface IFullTextQueryRow : IQueryRow
    {
        #region Properties

        /// <summary>
        /// Gets the full text that was matched
        /// </summary>
        string FullTextMatched { get; }

        /// <summary>
        /// Gets the number of matches
        /// </summary>
        uint MatchCount { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the index of the term that was matched in the given match number
        /// </summary>
        /// <param name="matchNumber">The match number to check</param>
        /// <returns>The index of the term that was matched</returns>
        /// <exception cref = "System.IndexOutOfRangeException" ><c>matchNumber</c> was not between
        /// 0 and <see cref="MatchCount"/></exception>
        uint GetTermIndex(uint matchNumber);

        /// <summary>
        /// Gets the range in the string for the given full text match
        /// </summary>
        /// <param name="matchNumber">The match number to use</param>
        /// <returns>The range in the string</returns>
        /// <exception cref="System.IndexOutOfRangeException"><c>matchNumber</c> was not between
        /// 0 and <see cref="MatchCount"/></exception>
        Range GetTextRange(uint matchNumber);

        #endregion
    }
}

// 
// IConflictResolver.cs
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
    /// An interface for resolving a <see cref="Conflict" /> in a document (i.e. two edits to the same
    /// document at the same time)
    /// </summary>
    public interface IConflictResolver
    {
        #region Public Methods

        /// <summary>
        /// Resolves a <see cref="Conflict"/> in a <see cref="Document"/>
        /// </summary>
        /// <param name="conflict">The conflict that occurred</param>
        /// <returns>The new version of the document</returns>
        Document Resolve(Conflict conflict);

        #endregion
    }

    internal sealed class DefaultConflictResolver : IConflictResolver
    {
        #region IConflictResolver

        /// <inheritdoc />
        public Document Resolve(Conflict conflict)
        {
            // Default resolution algorithm is "most active wins", i.e. higher generation number
            var mine = conflict.Mine;
            var theirs = conflict.Theirs;
            return mine.Generation >= theirs.Generation ? mine : theirs;
        }

        #endregion
    }
}

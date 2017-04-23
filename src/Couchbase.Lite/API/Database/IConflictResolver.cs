//
//  IConflictResolver.cs
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
    public enum OperationType
    {
        DatabaseWrite,
        PushReplication,
        PullReplication
    }

    public interface IConflict
    {
        IReadOnlyDocument Source { get; }
        
        IReadOnlyDocument Target { get; }

        IReadOnlyDocument CommonAncestor { get; }

        OperationType OperationType { get; }
    }

    /// <summary>
    /// An interface for resolving a conflict in a document (i.e. two edits to the same
    /// document at the same time)
    /// </summary>
    public interface IConflictResolver
    {
        #region Public Methods

        IReadOnlyDocument Resolve(IConflict conflict);

        #endregion
    }

    public sealed class MostActiveWinsConflictResolver : IConflictResolver
    {
        public IReadOnlyDocument Resolve(IConflict conflict)
        {
            throw new System.NotImplementedException();
        }
    }
}

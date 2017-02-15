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

using System.Collections.Generic;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface for resolving a conflict in a document (i.e. two edits to the same
    /// document at the same time)
    /// </summary>
    public interface IConflictResolver
    {
        #region Public Methods

        /// <summary>
        /// Resolves the conflict between the given items
        /// </summary>
        /// <param name="mine">The properties on the document that the local user created</param>
        /// <param name="theirs">The properties on the document that exist from another edit</param>
        /// <param name="baseProps">The properties as they were before either edit</param>
        /// <returns>The resolved document properties, or <c>null</c> if the document cannot be resolved</returns>
        /// <exception cref="LiteCore.LiteCoreException">Thrown if the method fails to resolve the document 
        /// by returning null</exception>
        IDictionary<string, object> Resolve(IReadOnlyDictionary<string, object> mine,
            IReadOnlyDictionary<string, object> theirs, IReadOnlyDictionary<string, object> baseProps);

        #endregion
    }
}

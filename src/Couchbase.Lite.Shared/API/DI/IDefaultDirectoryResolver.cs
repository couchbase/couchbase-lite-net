//
//  IDefaultPathResolver.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite.DI
{

    /// <summary>
    /// An interface for resolving the default directory for a Couchbase Lite database
    /// since we may be operating in a sandboxed environment
    /// </summary>
    public interface IDefaultDirectoryResolver
    {
        #region Public Methods

        /// <summary>
        /// Gets the default directory for a Couchbase Lite database to live in
        /// </summary>
        /// <returns>The default directory for a Couchbase Lite database to live in</returns>
        [NotNull]
        string DefaultDirectory();

        #endregion
    }
}

//
//  CollectionChangedEventArgs.cs
//
//  Copyright (c) 2022 Couchbase, Inc All rights reserved.
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Couchbase.Lite
{
    /// <summary>
    /// The parameters of a collection changed event
    /// </summary>
    public sealed class CollectionChangedEventArgs : EventArgs
    {
        #region Properties

        /// <summary>
        /// Gets the collection in which the change occurred
        /// </summary>
        [NotNull]
        public Collection Collection { get; }

        /// <summary>
        /// Gets the document that was changed
        /// </summary>
        [NotNull]
        [ItemNotNull]
        public IReadOnlyList<string> DocumentIDs { get; }

        #endregion

        #region Constructors

        internal CollectionChangedEventArgs([NotNull] Collection collection, [NotNull][ItemNotNull] IReadOnlyList<string> documentIDs)
        {
            Debug.Assert(collection != null);
            Debug.Assert(documentIDs != null);
            Debug.Assert(documentIDs.All(x => x != null));
            Collection = collection;
            DocumentIDs = documentIDs;
        }

        #endregion
    }
}

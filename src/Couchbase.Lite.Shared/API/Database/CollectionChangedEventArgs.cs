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

using System;
using System.Collections.Generic;
using System.Diagnostics;

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
        public Collection Collection { get; }

        /// <summary>
        /// Gets the document that was changed
        /// </summary>
        public IReadOnlyList<string> DocumentIDs { get; }

        #endregion

        #region Constructors

        internal CollectionChangedEventArgs(Collection collection, IReadOnlyList<string> documentIDs)
        {
            DocumentIDs = documentIDs; 
            Collection = collection;
        }

        #endregion
    }
}

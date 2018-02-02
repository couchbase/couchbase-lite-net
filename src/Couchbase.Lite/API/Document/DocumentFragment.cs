// 
//  DocumentFragment.cs
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

using Couchbase.Lite.Internal.Doc;

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// DocumentFragment provides access to a <see cref="Document"/> object.  It also provides subscript access
    /// by key to the data values of the wrapped document.
    /// </summary>
    public sealed class DocumentFragment : IDictionaryFragment
    {
        #region Properties

        /// <summary>
        /// Gets the <see cref="Document"/> from the document fragment
        /// </summary>
        [CanBeNull]
        public Document Document { get; }

        /// <summary>
        /// Gets whether or not this document is in the database
        /// </summary>
        public bool Exists => Document != null;

        /// <inheritdoc />
        public IFragment this[string key] => Document?[key] ?? Fragment.Null;

        #endregion

        #region Constructors

        internal DocumentFragment(Document doc)
        {
            Document = doc;
        }

        #endregion
    }
}
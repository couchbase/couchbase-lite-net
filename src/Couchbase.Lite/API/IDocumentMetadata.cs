//
//  IDocumentMetadata.cs
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
    /// An interface describing metadata for an <see cref="IDocumentModel"/>
    /// </summary>
    public interface IDocumentMetadata
    {
        /// <summary>
        /// Gets the unique ID of the document
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets or sets the type of the document
        /// </summary>
        string Type { get; set; }

        /// <summary>
        /// Gets whether or not the document is deleted
        /// </summary>
        bool IsDeleted { get; }

        /// <summary>
        /// Gets the unique sequence number of the document
        /// </summary>
        ulong Sequence { get; }
    }
}

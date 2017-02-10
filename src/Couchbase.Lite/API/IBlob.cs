//
//  IBlob.cs
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
using System.IO;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface describing a typed binary data object
    /// </summary>
    public interface IBlob
    {
        /// <summary>
        /// Gets the content as an in memory array
        /// </summary>
        byte[] Content { get; }

        /// <summary>
        /// Gets the content as a stream from the soruce
        /// </summary>
        Stream ContentStream { get; }

        /// <summary>
        /// Gets the content type of the object (e.g. application/x-octet)
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// Gets the length of the data contained in the object
        /// </summary>
        ulong Length { get; }

        /// <summary>
        /// Gets the digest of the object
        /// </summary>
        string Digest { get; }

        /// <summary>
        /// Gets the metadata about the object
        /// </summary>
        IReadOnlyDictionary<string, object> Properties { get; }
    }
}

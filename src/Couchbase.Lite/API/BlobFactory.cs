//
//  BlobFactory.cs
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

using System;
using System.IO;

using Couchbase.Lite.DB;

namespace Couchbase.Lite
{
    /// <summary>
    /// A factory for creating IBlob objects
    /// </summary>
    public static class BlobFactory
    {
        /// <summary>
        /// Creates an <see cref="IBlob" /> given a type and in memory content
        /// </summary>
        /// <param name="contentType">The binary type of the blob</param>
        /// <param name="content">The content of the blob</param>
        /// <returns>An instantiated <see cref="IBlob" /> object</returns>
        /// <exception cref="ArgumentNullException">Thrown if <c>content</c> is <c>null</c></exception>
        public static IBlob Create(string contentType, byte[] content)
        {
            return new Blob(contentType, content);
        }

        /// <summary>
        /// Creates an <see cref="IBlob" /> given a type and streaming content
        /// </summary>
        /// <param name="contentType">The binary type of the blob</param>
        /// <param name="stream">The stream containing the blob content</param>
        /// <returns>An instantiated <see cref="IBlob" /> object</returns>
        /// <exception cref="ArgumentNullException">Thrown if <c>stream</c> is <c>null</c></exception>
        public static IBlob Create(string contentType, Stream stream)
        {
            return new Blob(contentType, stream);
        }

        /// <summary>
        /// Creates an <see cref="IBlob" /> given a type and a URL to a file
        /// </summary>
        /// <param name="contentType">The binary type of the blob</param>
        /// <param name="fileUrl">The url to the file to read</param>
        /// <returns>An instantiated <see cref="IBlob" /> object</returns>
        /// <exception cref="ArgumentNullException">Thrown if <c>fileUrl</c> is <c>null</c></exception>
        /// <exception cref="ArgumentException">Thrown if fileUrl is not a file based URL</exception>
        /// <exception cref="DirectoryNotFoundException">The specified fileUrl is invalid, 
        /// (for example, it is on an unmapped drive).</exception>
        /// <exception cref="UnauthorizedAccessException">fileUrl specified a directory -or- The caller 
        /// does not have the required permission.</exception>
        /// <exception cref="FileNotFoundException">The file specified in fileUrl was not found.</exception>
        /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
        public static IBlob Create(string contentType, Uri fileUrl)
        {
            return new Blob(contentType, fileUrl);
        }
    }
}

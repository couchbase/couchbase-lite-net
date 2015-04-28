//
//  ICouchbaseResponseWriter.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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

using System.Text;
using System.IO;

namespace Couchbase.Lite.Listener
{

    /// <summary>
    /// An interface for writing a response to a (presumably) network connection
    /// </summary>
    public interface ICouchbaseResponseWriter
    {

        /// <summary>
        /// Gets or sets the content encoding.
        /// </summary>
        Encoding ContentEncoding { get; set; }

        /// <summary>
        /// Gets or sets the length of the content.
        /// </summary>
        long ContentLength { get; set; }

        /// <summary>
        /// Gets the output stream for writing content to
        /// </summary>
        Stream OutputStream { get; }

        /// <summary>
        /// Gets or sets the status code of the response
        /// </summary>
        int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the status description of the response.
        /// </summary>
        string StatusDescription { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance will
        /// send data all at once, or piece by piece
        /// </summary>
        bool IsChunked { get; set; }

        /// <summary>
        /// Adds a header to the header collection for the response
        /// </summary>
        /// <param name="name">The name of the header</param>
        /// <param name="value">The value of the header</param>
        void AddHeader(string name, string value);

        /// <summary>
        /// Clears the headers
        /// </summary>
        void ClearHeaders();

        /// <summary>
        /// Closes the response, causing the client connection to end
        /// </summary>
        void Close();

    }
}


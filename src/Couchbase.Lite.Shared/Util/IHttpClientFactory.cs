//
// IHttpClientFactory.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Net.Http;
using System.Collections.Generic;
using Couchbase.Lite.Auth;

#if NET_3_5
using System.Net.Couchbase;
#else
using System.Net;
#endif

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// An interface describing an object capable of creating and customizing 
    /// an HttpClient object
    /// </summary>
    public interface IHttpClientFactory
    {
        /// <summary>
        /// Gets the HttpClient object for use in replication
        /// </summary>
        /// <param name="chunkedMode">A flag for chunked mode (i.e. the connection stays open for heartbeat, etc)</param>
        /// <returns>The http client.</returns>
        HttpClient GetHttpClient(bool chunkedMode);

        /// <summary>
        /// Gets the HttpClient object for use in replication
        /// </summary>
        /// <param name="chunkedMode">A flag for chunked mode (i.e. the connection stays open for heartbeat, etc)</param>
        /// <param name="retry">A flag to enable/disable the retry handler</param>
        /// <returns>The http client.</returns>
        HttpClient GetHttpClient(bool chunkedMode, bool retry);

        /// <summary>
        /// Gets or sets the headers used by default in the HttpClient
        /// </summary>
        /// <value>The headers.</value>
        IDictionary<string,string> Headers { get; set; }

        /// <summary>
        /// Gets the handler used in the HttpClient
        /// </summary>
        /// <value>The handler.</value>
        MessageProcessingHandler Handler { get; }

        /// <summary>
        /// Adds default cookies to the HttpClient
        /// </summary>
        /// <param name="cookies">The cookies to add</param>
        void AddCookies(CookieCollection cookies);

        /// <summary>
        /// Deletes cookies from the HttpClient
        /// </summary>
        /// <param name="domain">The domain to search for the cookie</param>
        /// <param name="name">The name of the cookie</param>
        void DeleteCookie(Uri domain, string name);

        /// <summary>
        /// Gets the container holding the cookies for the HttpClient
        /// </summary>
        /// <returns>The cookie container.</returns>
        CookieContainer GetCookieContainer();
    }
}


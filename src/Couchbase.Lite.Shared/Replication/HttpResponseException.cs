//
// HttpResponseException.cs
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
using System.Net;
using System.Runtime.Serialization;

namespace Couchbase.Lite
{
    /// <summary>
    /// An exception for encapsulating HTTP errors
    /// </summary>
    [Serializable]
    public class HttpResponseException : Exception
    {

        /// <summary>
        /// Gets or sets the status code associated with the error
        /// </summary>
        /// <value>The status code.</value>
        public HttpStatusCode StatusCode { get; set; }

        internal HttpResponseException (HttpStatusCode statusCode) { StatusCode = statusCode; }

        internal HttpResponseException(string message) : this(message, null)
        {
            
        }

        internal HttpResponseException(string message, Exception innerException)
            : base(message, innerException)
        {
            StatusCode = HttpStatusCode.InternalServerError;
        }

        protected HttpResponseException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            StatusCode = (HttpStatusCode)info.GetInt32("HttpResponseStatusCode");
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents the current <see cref="Couchbase.Lite.HttpResponseException"/>.
        /// </summary>
        /// <returns>A <see cref="System.String"/> that represents the current <see cref="Couchbase.Lite.HttpResponseException"/>.</returns>
        public override string ToString ()
        {
            return string.Format ("[HttpResponseException: StatusCode = {0}]", StatusCode);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("HttpResponseStatusCode", (int)StatusCode);
        }
    }
}


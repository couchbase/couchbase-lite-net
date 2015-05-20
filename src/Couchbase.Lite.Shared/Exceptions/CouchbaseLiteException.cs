//
// CouchbaseLiteException.cs
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

namespace Couchbase.Lite {

    /// <summary>
    /// The main class of exception used for indicating Couchbase Lite errors
    /// </summary>
    public class CouchbaseLiteException : ApplicationException {

        internal StatusCode Code { get; set; }

        /// <summary>
        /// Gets the Status object holding the error code for this exception
        /// </summary>
        public Status CBLStatus {
            get {
                return new Status(Code);
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public CouchbaseLiteException() : this(StatusCode.Unknown) {  }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">The message to use</param>
        /// <param name="innerException">The exception that was caught before the one being made, if applicable</param>
        public CouchbaseLiteException(string message, Exception innerException) : base(message, innerException) { Code = StatusCode.Unknown; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="innerException">The exception that was caught before the one being made, if applicable</param>
        /// <param name="code">The status code representing the details of the error</param>
        public CouchbaseLiteException (Exception innerException, StatusCode code) : base(String.Format("Database error: {0}", code), innerException) { Code = code; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="innerException">The exception that was caught before the one being made, if applicable</param>
        /// <param name="status">The object holding the code representing the error for this exception</param>
        public CouchbaseLiteException (Exception innerException, Status status) : this(innerException, status.Code) { Code = status.Code; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="code">The status code representing the details of the error</param>
        public CouchbaseLiteException (StatusCode code) : base(String.Format("Coucbase Lite error: {0}", code)) { Code = code; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="code">The status code representing the details of the error</param>
        public CouchbaseLiteException (String message, StatusCode code) : base(message) { Code = code; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">The message to display</param>
        public CouchbaseLiteException (String message) : base(message) {  }

        /// <summary>
        /// Initializes a new instance of the <see cref="Couchbase.Lite.CouchbaseLiteException"/> class.
        /// </summary>
        /// <param name="messageFormat">Message format.</param>
        /// <param name="values">Values.</param>
        public CouchbaseLiteException (String messageFormat, params Object[] values)
            : base(String.Format(messageFormat, values)) {  }

        /// <summary>
        /// Gets the Status object holding the error code for this exception
        /// </summary>
        /// <returns>the Status object holding the error code for this exception</returns>
        [Obsolete("Use the CBLStatus property instead")]
        public Status GetCBLStatus ()
        {
            return new Status(Code);
        }
    }

}

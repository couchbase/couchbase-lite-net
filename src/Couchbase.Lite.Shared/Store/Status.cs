//
// Status.cs
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

using Sharpen;
using System;

namespace Couchbase.Lite
{
    /// <summary>Same interpretation as HTTP status codes, esp.</summary>
    /// <remarks>Same interpretation as HTTP status codes, esp. 200, 201, 404, 409, 500.</remarks>
    public enum StatusCode
    {
        Unknown = -1,

        Ok = 200,

        Created = 201,

        NotModified = 304,

        BadRequest = 400,

        Unauthorized = 401,

        Forbidden = 403,

        NotFound = 404,

        MethodNotAllowed = 405,

        NotAcceptable = 406,

        Conflict = 409,

        PreconditionFailed = 412,

        BadEncoding = 490,

        BadAttachment = 491,

        BadJson = 493,

        InternalServerError = 500,

        UpStreamError = 589,

        StatusAttachmentError = 592,

        DbError = 590,

        DbBusy = 595
    }

    public class Status {

        private StatusCode code;

        public Status()
        {
            this.code = StatusCode.Unknown;
        }

        public Status(StatusCode code)
        {
            this.code = code;
        }

        public virtual StatusCode GetCode()
        {
            return code;
        }

        public virtual void SetCode(StatusCode code)
        {
            this.code = code;
        }

        public virtual Boolean IsSuccessful()
        {
            return ((Int32)code > 0 && (Int32)code < 400);
        }

        public override string ToString()
        {
            return "Status: " + code;
        }
    }
}

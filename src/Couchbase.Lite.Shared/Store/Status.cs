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
using System.Collections.Generic;

namespace Couchbase.Lite
{
    /// <summary>Same interpretation as HTTP status codes, esp.</summary>
    /// <remarks>Same interpretation as HTTP status codes, esp. 200, 201, 404, 409, 500.</remarks>
    public enum StatusCode
    {
        Unknown = -1,

        Ok = 200,

        Created = 201,

        Accepted = 202,

        NotModified = 304,

        BadRequest = 400,

        Unauthorized = 401,

        Forbidden = 403,

        NotFound = 404,

        MethodNotAllowed = 405,

        NotAcceptable = 406,

        Conflict = 409,

        PreconditionFailed = 412,

        UnsupportedType = 415,

        BadEncoding = 490,

        BadAttachment = 491,

        AttachmentNotFound = 492,

        BadJson = 493,

        BadId = 494,

        BadParam = 495,

        Deleted = 496,

        InternalServerError = 500,

        NotImplemented = 501,

        BadChangesFeed = 587,

        ChangesFeedTruncated = 588,

        UpStreamError = 589,

        DbError = 590,

        CorruptError = 591,

        AttachmentError = 592,

        CallbackError = 593,

        Exception = 594,

        DbBusy = 595
    }

    public class Status {

        private StatusCode code;
        private static readonly Dictionary<StatusCode, Tuple<int, string>> _StatusMap =
            new Dictionary<StatusCode, Tuple<int, string>>
        {
            // For compatibility with CouchDB, return the same strings it does (see couch_httpd.erl)
            { StatusCode.BadRequest, Tuple.Create(400, "bad_request") },
            { StatusCode.Unauthorized, Tuple.Create(401, "unauthorized") },
            { StatusCode.NotFound, Tuple.Create(404, "not_found") },
            { StatusCode.Forbidden, Tuple.Create(403, "forbidden") },
            { StatusCode.MethodNotAllowed, Tuple.Create(405, "method_not_allowed") },
            { StatusCode.NotAcceptable, Tuple.Create(406, "not_acceptable") },
            { StatusCode.Conflict, Tuple.Create(409, "conflict") },
            { StatusCode.PreconditionFailed, Tuple.Create(412, "file_exists") },
            { StatusCode.UnsupportedType, Tuple.Create(415, "bad_content_type") },

            { StatusCode.Ok, Tuple.Create(200, "ok") },
            { StatusCode.Created, Tuple.Create(201, "created") },
            { StatusCode.Accepted, Tuple.Create(202, "accepted") },

            { StatusCode.NotModified, Tuple.Create(304, "not_modified") },

            // These are nonstandard status codes; map them to closest HTTP equivalents:
            { StatusCode.BadEncoding, Tuple.Create(400, "Bad data encoding") },
            { StatusCode.BadAttachment, Tuple.Create(400, "Bad attachment") },
            { StatusCode.AttachmentNotFound, Tuple.Create(404, "Attachment not found") },
            { StatusCode.BadJson, Tuple.Create(400, "Bad JSON") },
            { StatusCode.BadId, Tuple.Create(400, "Invalid database/document/revision ID") },
            { StatusCode.BadParam, Tuple.Create(400, "Invalid parameter in HTTP query or JSON body") },
            { StatusCode.Deleted, Tuple.Create(404, "Deleted") },

            { StatusCode.UpStreamError, Tuple.Create(502, "Invalid response from remote replication server") },
            { StatusCode.BadChangesFeed, Tuple.Create(502, "Server changes feed parse error") },
            { StatusCode.ChangesFeedTruncated, Tuple.Create(502, "Server changes feed truncated") },
            { StatusCode.NotImplemented, Tuple.Create(501, "Not implemented") },
            { StatusCode.DbError, Tuple.Create(500, "Database error!") },
            { StatusCode.CorruptError, Tuple.Create(500, "Invalid data in database") },
            { StatusCode.AttachmentError, Tuple.Create(500, "Attachment store error") },
            { StatusCode.CallbackError, Tuple.Create(500, "Application callback block failed") },
            { StatusCode.Exception, Tuple.Create(500, "Internal error") },
            { StatusCode.DbBusy, Tuple.Create(500, "Database locked") }
        };

        public Status()
        {
            // SQLite DB is busy (this is recoverable!)
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

        public Boolean IsSuccessful
        {
            get { return ((Int32)code > 0 && (Int32)code < 400); }
        }

        public Boolean IsError
        {
            get { return !IsSuccessful; }
        }

        public override string ToString()
        {
            return "Status: " + code;
        }

        public static Tuple<int, string> ToHttpStatus(StatusCode status)
        {
            Tuple<int, string> retVal;
            if (_StatusMap.TryGetValue(status, out retVal)) {
                return retVal;
            }

            return Tuple.Create(-1, string.Empty);
        }
    }
}

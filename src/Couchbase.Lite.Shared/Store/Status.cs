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

using System;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    /// <summary>
    /// A list of statuses indicating various results and/or errors for Couchbase Lite
    /// operations
    /// </summary>
    [Serializable]
    public enum StatusCode
    {
        /// <summary>
        /// Unknown result (should not be used)
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// Successful completion (HTTP compliant)
        /// </summary>
        Ok = 200,

        /// <summary>
        /// A new item was created (HTTP compliant)
        /// </summary>
        Created = 201,

        /// <summary>
        /// The operation has been successfully queued for execution (HTTP compliant)
        /// </summary>
        Accepted = 202,

        /// <summary>
        /// The requested action is redundant and doesn't need to execute (HTTP compliant)
        /// </summary>
        NotModified = 304,

        /// <summary>
        /// An invalid request was received (HTTP compliant)
        /// </summary>
        BadRequest = 400,

        /// <summary>
        /// The requesting user is not authorized to perform the action (HTTP compliant)
        /// </summary>
        Unauthorized = 401,

        /// <summary>
        /// The requested action is not allowed to be executed by any user (HTTP compliant)
        /// </summary>
        Forbidden = 403,

        /// <summary>
        /// The requested item does not appear to exist (HTTP compliant)
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// The wrong HTTP method was used when requesting an action to be performed (HTTP compliant)
        /// </summary>
        MethodNotAllowed = 405,

        /// <summary>
        /// The server is unable to return an acceptable MIME type as specified in the HTTP headers (HTTP compliant)
        /// </summary>
        NotAcceptable = 406,

        /// <summary>
        /// The submitted revision put a document into a conflict state (HTTP compliant)
        /// </summary>
        Conflict = 409,

        /// <summary>
        /// A condition of the requested action was violated (e.g. Trying to create a DB when it already exists) (HTTP compliant)
        /// </summary>
        PreconditionFailed = 412,

        /// <summary>
        /// The server does not support this type of file (HTTP compliant)
        /// </summary>
        UnsupportedType = 415,

        /// <summary>
        /// The encoding type for the attachment on a revision is not supported
        /// </summary>
        BadEncoding = 490,

        /// <summary>
        /// The received attachment is corrupt
        /// </summary>
        BadAttachment = 491,

        /// <summary>
        /// The attachment for the revision was not received
        /// </summary>
        AttachmentNotFound = 492,

        /// <summary>
        /// The received JSON was invalid
        /// </summary>
        BadJson = 493,

        /// <summary>
        /// A parameter was received that doesn't make sense for the action
        /// </summary>
        BadId = 494,

        /// <summary>
        /// An invalid parameter was received
        /// </summary>
        BadParam = 495,

        /// <summary>
        /// The document has been deleted
        /// </summary>
        Deleted = 496,

        /// <summary>
        /// Internal logic error (i.e. library problem) (HTTP compliant)
        /// </summary>
        InternalServerError = 500,

        /// <summary>
        /// The logic has not been implemented yet (HTTP compliant)
        /// </summary>
        NotImplemented = 501,

        /// <summary>
        /// An invalid changes feed was received from Sync Gateway
        /// </summary>
        BadChangesFeed = 587,

        /// <summary>
        /// The changes feed from Sync Gateway was cut off
        /// </summary>
        ChangesFeedTruncated = 588,

        /// <summary>
        /// An error was received fro Sync Gateway
        /// </summary>
        UpStreamError = 589,

        /// <summary>
        /// A database error occurred (file locked, etc)
        /// </summary>
        DbError = 590,

        /// <summary>
        /// A corrupt database was found
        /// </summary>
        CorruptError = 591,

        /// <summary>
        /// A problem with an attachment was found
        /// </summary>
        AttachmentError = 592,

        /// <summary>
        /// A callback failed
        /// </summary>
        CallbackError = 593,

        /// <summary>
        /// Releated to 500, but not HTTP compliant
        /// </summary>
        Exception = 594,

        /// <summary>
        /// The database file is busy, and cannot process changes at the moment
        /// </summary>
        DbBusy = 595
    }

    /// <summary>
    /// A class for encapsulating a status code, and querying various information about it
    /// </summary>
    public class Status 
    {

        #region Constants

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

        #endregion

        #region Properties

        /// <summary>
        /// The status code that this object holds
        /// </summary>
        public StatusCode Code { get; set; }

        /// <summary>
        /// Gets whether or not the status code represents a successful action
        /// </summary>
        public Boolean IsSuccessful
        {
            get { return ((Int32)Code > 0 && (Int32)Code < 400); }
        }

        /// <summary>
        /// Gets whether or not the status code represents a failed action
        /// </summary>
        public Boolean IsError
        {
            get { return !IsSuccessful; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Convenience constructor
        /// </summary>
        public Status()
        {
            this.Code = StatusCode.Unknown;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="code">The status code to hold</param>
        public Status(StatusCode code)
        {
            this.Code = code;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the status code
        /// </summary>
        /// <returns>The status code</returns>
        [Obsolete("Use the Code property")]
        public virtual StatusCode GetCode()
        {
            return Code;
        }

        /// <summary>
        /// Modifies the status code being held by this object
        /// </summary>
        /// <param name="code">The new status code</param>
        [Obsolete("Use the Code property")]
        public virtual void SetCode(StatusCode code)
        {
            this.Code = code;
        }

        /// <summary>
        /// Converts a StatusCode to an HTTP compliant status
        /// </summary>
        /// <returns>A tuple containing the status code, and message to return to the client</returns>
        /// <param name="status">The status code to convert</param>
        public static Tuple<int, string> ToHttpStatus(StatusCode status)
        {
            Tuple<int, string> retVal;
            if (_StatusMap.TryGetValue(status, out retVal)) {
                return retVal;
            }

            return Tuple.Create(-1, string.Empty);
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents the current <see cref="Couchbase.Lite.Status"/>.
        /// </summary>
        /// <returns>A <see cref="System.String"/> that represents the current <see cref="Couchbase.Lite.Status"/>.</returns>
        public override string ToString()
        {
            return String.Format("Status: {0}", Code);
        }

        #endregion

    }
}

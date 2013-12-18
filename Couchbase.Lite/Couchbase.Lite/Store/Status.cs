/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

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

		StatusAttachmentError = 592,

		DbError = 590
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

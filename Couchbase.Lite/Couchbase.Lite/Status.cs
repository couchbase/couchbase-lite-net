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

namespace Com.Couchbase.Lite
{
	/// <summary>Same interpretation as HTTP status codes, esp.</summary>
	/// <remarks>Same interpretation as HTTP status codes, esp. 200, 201, 404, 409, 500.</remarks>
	public class Status
	{
		public const int Unknown = -1;

		public const int Ok = 200;

		public const int Created = 201;

		public const int NotModified = 304;

		public const int BadRequest = 400;

		public const int Forbidden = 403;

		public const int NotFound = 404;

		public const int MethodNotAllowed = 405;

		public const int NotAcceptable = 406;

		public const int Conflict = 409;

		public const int PreconditionFailed = 412;

		public const int BadEncoding = 490;

		public const int BadAttachment = 491;

		public const int BadJson = 493;

		public const int InternalServerError = 500;

		public const int StatusAttachmentError = 592;

		public const int DbError = 590;

		private int code;

		public Status()
		{
			this.code = Unknown;
		}

		public Status(int code)
		{
			this.code = code;
		}

		public virtual int GetCode()
		{
			return code;
		}

		public virtual void SetCode(int code)
		{
			this.code = code;
		}

		public virtual bool IsSuccessful()
		{
			return (code > 0 && code < 400);
		}

		public override string ToString()
		{
			return "Status: " + code;
		}
	}
}

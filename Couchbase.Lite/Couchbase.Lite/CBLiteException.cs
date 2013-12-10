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

using System;
using Couchbase;
using Sharpen;

namespace Couchbase
{
	[System.Serializable]
	public class CBLiteException : Exception
	{
		private CBLStatus status;

		public CBLiteException(CBLStatus status)
		{
			this.status = status;
		}

		public CBLiteException(string detailMessage, CBLStatus status) : base(detailMessage
			)
		{
			this.status = status;
		}

		public CBLiteException(string detailMessage, Exception throwable, CBLStatus status
			) : base(detailMessage, throwable)
		{
			this.status = status;
		}

		public CBLiteException(Exception throwable, CBLStatus status) : base(throwable)
		{
			this.status = status;
		}

		public virtual CBLStatus GetCBLStatus()
		{
			return status;
		}
	}
}

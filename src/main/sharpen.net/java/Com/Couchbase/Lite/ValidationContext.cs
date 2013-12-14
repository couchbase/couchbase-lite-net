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

using Com.Couchbase.Lite;
using Com.Couchbase.Lite.Internal;
using Sharpen;

namespace Com.Couchbase.Lite
{
	/// <summary>Context passed into a ValidationBlock.</summary>
	/// <remarks>Context passed into a ValidationBlock.</remarks>
	public interface ValidationContext
	{
		/// <summary>The contents of the current revision of the document, or nil if this is a new document.
		/// 	</summary>
		/// <remarks>The contents of the current revision of the document, or nil if this is a new document.
		/// 	</remarks>
		/// <exception cref="Com.Couchbase.Lite.CouchbaseLiteException"></exception>
		RevisionInternal GetCurrentRevision();

		/// <summary>The type of HTTP status to report, if the validate block returns NO.</summary>
		/// <remarks>
		/// The type of HTTP status to report, if the validate block returns NO.
		/// The default value is 403 ("Forbidden").
		/// </remarks>
		Status GetErrorType();

		void SetErrorType(Status status);

		/// <summary>The error message to return in the HTTP response, if the validate block returns NO.
		/// 	</summary>
		/// <remarks>
		/// The error message to return in the HTTP response, if the validate block returns NO.
		/// The default value is "invalid document".
		/// </remarks>
		string GetErrorMessage();

		void SetErrorMessage(string message);
	}
}

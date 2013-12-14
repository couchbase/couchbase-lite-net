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

using Org.Apache.Http.Protocol;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>HttpContext represents execution state of an HTTP process.</summary>
	/// <remarks>
	/// HttpContext represents execution state of an HTTP process. It is a structure
	/// that can be used to map an attribute name to an attribute value.
	/// <p/>
	/// The primary purpose of the HTTP context is to facilitate information sharing
	/// among various  logically related components. HTTP context can be used
	/// to store a processing state for one message or several consecutive messages.
	/// Multiple logically related messages can participate in a logical session
	/// if the same context is reused between consecutive messages.
	/// <p>/
	/// IMPORTANT: Please note HTTP context implementation, even when thread safe,
	/// may not be used concurrently by multiple threads, as the context may contain
	/// thread unsafe attributes.
	/// </remarks>
	/// <since>4.0</since>
	public abstract class HttpContext
	{
		/// <summary>The prefix reserved for use by HTTP components.</summary>
		/// <remarks>The prefix reserved for use by HTTP components. "http."</remarks>
		public const string ReservedPrefix = "http.";

		/// <summary>Obtains attribute with the given name.</summary>
		/// <remarks>Obtains attribute with the given name.</remarks>
		/// <param name="id">the attribute name.</param>
		/// <returns>attribute value, or <code>null</code> if not set.</returns>
		public abstract object GetAttribute(string id);

		/// <summary>Sets value of the attribute with the given name.</summary>
		/// <remarks>Sets value of the attribute with the given name.</remarks>
		/// <param name="id">the attribute name.</param>
		/// <param name="obj">the attribute value.</param>
		public abstract void SetAttribute(string id, object obj);

		/// <summary>Removes attribute with the given name from the context.</summary>
		/// <remarks>Removes attribute with the given name from the context.</remarks>
		/// <param name="id">the attribute name.</param>
		/// <returns>attribute value, or <code>null</code> if not set.</returns>
		public abstract object RemoveAttribute(string id);
	}
}

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
using System.Collections.Generic;
using Org.Apache.Http;
using Org.Apache.Http.Protocol;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>Provides access to an ordered list of response interceptors.</summary>
	/// <remarks>
	/// Provides access to an ordered list of response interceptors.
	/// Lists are expected to be built upfront and used read-only afterwards
	/// for
	/// <see cref="HttpProcessor">processing</see>
	/// .
	/// </remarks>
	/// <since>4.0</since>
	[System.ObsoleteAttribute(@"(4.3)")]
	public interface HttpResponseInterceptorList
	{
		/// <summary>Appends a response interceptor to this list.</summary>
		/// <remarks>Appends a response interceptor to this list.</remarks>
		/// <param name="interceptor">the response interceptor to add</param>
		void AddResponseInterceptor(HttpResponseInterceptor interceptor);

		/// <summary>Inserts a response interceptor at the specified index.</summary>
		/// <remarks>Inserts a response interceptor at the specified index.</remarks>
		/// <param name="interceptor">the response interceptor to add</param>
		/// <param name="index">the index to insert the interceptor at</param>
		void AddResponseInterceptor(HttpResponseInterceptor interceptor, int index);

		/// <summary>Obtains the current size of this list.</summary>
		/// <remarks>Obtains the current size of this list.</remarks>
		/// <returns>the number of response interceptors in this list</returns>
		int GetResponseInterceptorCount();

		/// <summary>Obtains a response interceptor from this list.</summary>
		/// <remarks>Obtains a response interceptor from this list.</remarks>
		/// <param name="index">
		/// the index of the interceptor to obtain,
		/// 0 for first
		/// </param>
		/// <returns>
		/// the interceptor at the given index, or
		/// <code>null</code> if the index is out of range
		/// </returns>
		HttpResponseInterceptor GetResponseInterceptor(int index);

		/// <summary>Removes all response interceptors from this list.</summary>
		/// <remarks>Removes all response interceptors from this list.</remarks>
		void ClearResponseInterceptors();

		/// <summary>Removes all response interceptor of the specified class</summary>
		/// <param name="clazz">the class of the instances to be removed.</param>
		void RemoveResponseInterceptorByClass<_T0>(Type<_T0> clazz) where _T0:HttpResponseInterceptor;

		/// <summary>Sets the response interceptors in this list.</summary>
		/// <remarks>
		/// Sets the response interceptors in this list.
		/// This list will be cleared and re-initialized to contain
		/// all response interceptors from the argument list.
		/// If the argument list includes elements that are not response
		/// interceptors, the behavior is implementation dependent.
		/// </remarks>
		/// <param name="list">the list of response interceptors</param>
		void SetInterceptors<_T0>(IList<_T0> list);
	}
}

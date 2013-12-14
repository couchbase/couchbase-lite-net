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

using Org.Apache.Http.Pool;
using Sharpen;

namespace Org.Apache.Http.Pool
{
	/// <summary>
	/// Interface to control runtime properties of a
	/// <see cref="ConnPool{T, E}">ConnPool&lt;T, E&gt;</see>
	/// such as
	/// maximum total number of connections or maximum connections per route
	/// allowed.
	/// </summary>
	/// <?></?>
	/// <since>4.2</since>
	public interface ConnPoolControl<T>
	{
		void SetMaxTotal(int max);

		int GetMaxTotal();

		void SetDefaultMaxPerRoute(int max);

		int GetDefaultMaxPerRoute();

		void SetMaxPerRoute(T route, int max);

		int GetMaxPerRoute(T route);

		PoolStats GetTotalStats();

		PoolStats GetStats(T route);
	}
}

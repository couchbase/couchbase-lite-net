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

using Org.Apache.Http;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>
	/// The point of access to the statistics of an
	/// <see cref="HttpConnection">HttpConnection</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	public interface HttpConnectionMetrics
	{
		/// <summary>
		/// Returns the number of requests transferred over the connection,
		/// 0 if not available.
		/// </summary>
		/// <remarks>
		/// Returns the number of requests transferred over the connection,
		/// 0 if not available.
		/// </remarks>
		long GetRequestCount();

		/// <summary>
		/// Returns the number of responses transferred over the connection,
		/// 0 if not available.
		/// </summary>
		/// <remarks>
		/// Returns the number of responses transferred over the connection,
		/// 0 if not available.
		/// </remarks>
		long GetResponseCount();

		/// <summary>
		/// Returns the number of bytes transferred over the connection,
		/// 0 if not available.
		/// </summary>
		/// <remarks>
		/// Returns the number of bytes transferred over the connection,
		/// 0 if not available.
		/// </remarks>
		long GetSentBytesCount();

		/// <summary>
		/// Returns the number of bytes transferred over the connection,
		/// 0 if not available.
		/// </summary>
		/// <remarks>
		/// Returns the number of bytes transferred over the connection,
		/// 0 if not available.
		/// </remarks>
		long GetReceivedBytesCount();

		/// <summary>Return the value for the specified metric.</summary>
		/// <remarks>Return the value for the specified metric.</remarks>
		/// <param name="metricName">the name of the metric to query.</param>
		/// <returns>
		/// the object representing the metric requested,
		/// <code>null</code> if the metric cannot not found.
		/// </returns>
		object GetMetric(string metricName);

		/// <summary>Resets the counts</summary>
		void Reset();
	}
}

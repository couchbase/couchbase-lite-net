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
using Org.Apache.Http.Protocol;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>
	/// HTTP protocol processor is a collection of protocol interceptors that
	/// implements the 'Chain of Responsibility' pattern, where each individual
	/// protocol interceptor is expected to work on a particular aspect of the HTTP
	/// protocol the interceptor is responsible for.
	/// </summary>
	/// <remarks>
	/// HTTP protocol processor is a collection of protocol interceptors that
	/// implements the 'Chain of Responsibility' pattern, where each individual
	/// protocol interceptor is expected to work on a particular aspect of the HTTP
	/// protocol the interceptor is responsible for.
	/// <p>
	/// Usually the order in which interceptors are executed should not matter as
	/// long as they do not depend on a particular state of the execution context.
	/// If protocol interceptors have interdependencies and therefore must be
	/// executed in a particular order, they should be added to the protocol
	/// processor in the same sequence as their expected execution order.
	/// <p>
	/// Protocol interceptors must be implemented as thread-safe. Similarly to
	/// servlets, protocol interceptors should not use instance variables unless
	/// access to those variables is synchronized.
	/// </remarks>
	/// <since>4.0</since>
	public interface HttpProcessor : IHttpRequestInterceptor, HttpResponseInterceptor
	{
		// no additional methods
	}
}

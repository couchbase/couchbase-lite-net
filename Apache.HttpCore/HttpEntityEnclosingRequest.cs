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
	/// <summary>A request with an entity.</summary>
	/// <remarks>A request with an entity.</remarks>
	/// <since>4.0</since>
	public interface HttpEntityEnclosingRequest : IHttpRequest
	{
		/// <summary>Tells if this request should use the expect-continue handshake.</summary>
		/// <remarks>
		/// Tells if this request should use the expect-continue handshake.
		/// The expect continue handshake gives the server a chance to decide
		/// whether to accept the entity enclosing request before the possibly
		/// lengthy entity is sent across the wire.
		/// </remarks>
		/// <returns>
		/// true if the expect continue handshake should be used, false if
		/// not.
		/// </returns>
		bool ExpectContinue();

		/// <summary>Associates the entity with this request.</summary>
		/// <remarks>Associates the entity with this request.</remarks>
		/// <param name="entity">the entity to send.</param>
		void SetEntity(HttpEntity entity);

		/// <summary>Returns the entity associated with this request.</summary>
		/// <remarks>Returns the entity associated with this request.</remarks>
		/// <returns>entity</returns>
		HttpEntity GetEntity();
	}
}

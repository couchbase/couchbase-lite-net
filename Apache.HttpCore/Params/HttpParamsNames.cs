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

using System.Collections.Generic;
using Org.Apache.Http.Params;
using Sharpen;

namespace Org.Apache.Http.Params
{
	/// <summary>Gives access to the full set of parameter names.</summary>
	/// <remarks>Gives access to the full set of parameter names.</remarks>
	/// <seealso cref="HttpParams">HttpParams</seealso>
	/// <since>4.2</since>
	[System.ObsoleteAttribute(@"(4.3) use configuration classes provided 'org.apache.http.config' and 'org.apache.http.client.config'"
		)]
	public interface HttpParamsNames
	{
		/// <summary>
		/// Returns the current set of names;
		/// in the case of stacked parameters, returns the names
		/// from all the participating HttpParams instances.
		/// </summary>
		/// <remarks>
		/// Returns the current set of names;
		/// in the case of stacked parameters, returns the names
		/// from all the participating HttpParams instances.
		/// Changes to the underlying HttpParams are not reflected
		/// in the set - it is a snapshot.
		/// </remarks>
		/// <returns>the names, as a Set<String></returns>
		ICollection<string> GetNames();
	}
}

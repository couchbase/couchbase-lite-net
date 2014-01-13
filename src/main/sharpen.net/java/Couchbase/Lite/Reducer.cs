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
using Couchbase.Lite;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>Block container for the reduce callback function</summary>
	public interface Reducer
	{
		/// <summary>A "reduce" function called to summarize the results of a view.</summary>
		/// <remarks>A "reduce" function called to summarize the results of a view.</remarks>
		/// <param name="keys">An array of keys to be reduced (or null if this is a rereduce).
		/// 	</param>
		/// <param name="values">A parallel array of values to be reduced, corresponding 1::1 with the keys.
		/// 	</param>
		/// <param name="rereduce">true if the input values are the results of previous reductions.
		/// 	</param>
		/// <returns>The reduced value; almost always a scalar or small fixed-size object.</returns>
		object Reduce(IList<object> keys, IList<object> values, bool rereduce);
	}
}

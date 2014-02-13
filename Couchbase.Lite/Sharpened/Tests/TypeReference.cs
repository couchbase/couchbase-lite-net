//
// TypeReference.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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
using Sharpen;
using Sharpen.Reflect;

namespace Couchbase.Lite.Util
{
	/// <summary>
	/// This class is used to pass full generics type information, and
	/// avoid problems with type erasure (that basically removes most
	/// usable type references from runtime Class objects).
	/// </summary>
	/// <remarks>
	/// This class is used to pass full generics type information, and
	/// avoid problems with type erasure (that basically removes most
	/// usable type references from runtime Class objects).
	/// It is based on ideas from
	/// &lt;a href="http://gafter.blogspot.com/2006/12/super-type-tokens.html"
	/// &gt;http://gafter.blogspot.com/2006/12/super-type-tokens.html</a>,
	/// Additional idea (from a suggestion made in comments of the article)
	/// is to require bogus implementation of <code>Comparable</code>
	/// (any such generic interface would do, as long as it forces a method
	/// with generic type to be implemented).
	/// to ensure that a Type argument is indeed given.
	/// <p>
	/// Usage is by sub-classing: here is one way to instantiate reference
	/// to generic type <code>List&lt;Integer&gt;</code>:
	/// <pre>
	/// TypeReference ref = new TypeReference&lt;List&lt;Integer&gt;&gt;() { };
	/// </pre>
	/// which can be passed to methods that accept TypeReference.
	/// </remarks>
	public abstract class TypeReference<T> : Comparable<Couchbase.Lite.Util.TypeReference
		<T>>
	{
		internal readonly Type _type;

		protected internal TypeReference()
		{
			Type superClass = GetType().GetGenericSuperclass();
			if (superClass is Type)
			{
				// sanity check, should never happen
				throw new ArgumentException("Internal error: TypeReference constructed without actual type information"
					);
			}
			_type = ((ParameterizedType)superClass).GetActualTypeArguments()[0];
		}

		public virtual Type GetType()
		{
			return _type;
		}

		/// <summary>
		/// The only reason we define this method (and require implementation
		/// of <code>Comparable</code>) is to prevent constructing a
		/// reference without type information.
		/// </summary>
		/// <remarks>
		/// The only reason we define this method (and require implementation
		/// of <code>Comparable</code>) is to prevent constructing a
		/// reference without type information.
		/// </remarks>
		public virtual int CompareTo(Couchbase.Lite.Util.TypeReference<T> o)
		{
			// just need an implementation, not a good one... hence:
			return 0;
		}
	}
}

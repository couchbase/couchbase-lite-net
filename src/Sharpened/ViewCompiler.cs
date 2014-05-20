//
// ViewCompiler.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
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
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>An external object that knows how to map source code of some sort into executable functions.
	/// 	</summary>
	/// <remarks>An external object that knows how to map source code of some sort into executable functions.
	/// 	</remarks>
	public interface ViewCompiler
	{
		/// <summary>Compiles source code into a MapDelegate.</summary>
		/// <remarks>Compiles source code into a MapDelegate.</remarks>
		/// <param name="source">The source code to compile into a Mapper.</param>
		/// <param name="language">The language of the source.</param>
		/// <returns>A compiled Mapper.</returns>
		[InterfaceAudience.Public]
		Mapper CompileMap(string source, string language);

		/// <summary>Compiles source code into a ReduceDelegate.</summary>
		/// <remarks>Compiles source code into a ReduceDelegate.</remarks>
		/// <param name="source">The source code to compile into a Reducer.</param>
		/// <param name="language">The language of the source.</param>
		/// <returns>A compiled Reducer</returns>
		[InterfaceAudience.Public]
		Reducer CompileReduce(string source, string language);
	}
}

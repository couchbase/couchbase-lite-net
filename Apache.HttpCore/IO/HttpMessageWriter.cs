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
using Org.Apache.Http.IO;
using Sharpen;

namespace Org.Apache.Http.IO
{
	/// <summary>
	/// Abstract message writer intended to serialize HTTP messages to an arbitrary
	/// data sink.
	/// </summary>
	/// <remarks>
	/// Abstract message writer intended to serialize HTTP messages to an arbitrary
	/// data sink.
	/// </remarks>
	/// <since>4.0</since>
	public interface HttpMessageWriter<T> where T:HttpMessage
	{
		/// <summary>
		/// Serializes an instance of
		/// <see cref="Org.Apache.Http.HttpMessage">Org.Apache.Http.HttpMessage</see>
		/// to the underlying data
		/// sink.
		/// </summary>
		/// <param name="message">HTTP message</param>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		/// <exception cref="Org.Apache.Http.HttpException">in case of HTTP protocol violation
		/// 	</exception>
		void Write(T message);
	}
}

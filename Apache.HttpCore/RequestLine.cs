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
	/// The Request-Line begins with a method token, followed by the
	/// Request-URI and the protocol version, and ending with CRLF.
	/// </summary>
	/// <remarks>
	/// The Request-Line begins with a method token, followed by the
	/// Request-URI and the protocol version, and ending with CRLF. The
	/// elements are separated by SP characters. No CR or LF is allowed
	/// except in the final CRLF sequence.
	/// <pre>
	/// Request-Line   = Method SP Request-URI SP HTTP-Version CRLF
	/// </pre>
	/// </remarks>
	/// <since>4.0</since>
	public interface RequestLine
	{
		string GetMethod();

		ProtocolVersion GetProtocolVersion();

		string GetUri();
	}
}

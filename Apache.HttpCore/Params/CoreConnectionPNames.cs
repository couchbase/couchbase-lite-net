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

using Org.Apache.Http.Params;
using Sharpen;

namespace Org.Apache.Http.Params
{
	/// <summary>Defines parameter names for connections in HttpCore.</summary>
	/// <remarks>Defines parameter names for connections in HttpCore.</remarks>
	/// <since>4.0</since>
	[System.ObsoleteAttribute(@"(4.3) use configuration classes provided 'org.apache.http.config' and 'org.apache.http.client.config'"
		)]
	public abstract class CoreConnectionPNames
	{
		/// <summary>
		/// Defines the socket timeout (<code>SO_TIMEOUT</code>) in milliseconds,
		/// which is the timeout for waiting for data  or, put differently,
		/// a maximum period inactivity between two consecutive data packets).
		/// </summary>
		/// <remarks>
		/// Defines the socket timeout (<code>SO_TIMEOUT</code>) in milliseconds,
		/// which is the timeout for waiting for data  or, put differently,
		/// a maximum period inactivity between two consecutive data packets).
		/// A timeout value of zero is interpreted as an infinite timeout.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="int">int</see>
		/// .
		/// </p>
		/// </remarks>
		/// <seealso cref="Sharpen.SocketOptions.SoTimeout">Sharpen.SocketOptions.SoTimeout</seealso>
		public const string SoTimeout = "http.socket.timeout";

		/// <summary>Determines whether Nagle's algorithm is to be used.</summary>
		/// <remarks>
		/// Determines whether Nagle's algorithm is to be used. The Nagle's algorithm
		/// tries to conserve bandwidth by minimizing the number of segments that are
		/// sent. When applications wish to decrease network latency and increase
		/// performance, they can disable Nagle's algorithm (that is enable
		/// TCP_NODELAY). Data will be sent earlier, at the cost of an increase
		/// in bandwidth consumption.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="bool">bool</see>
		/// .
		/// </p>
		/// </remarks>
		/// <seealso cref="Sharpen.SocketOptions.TcpNodelay">Sharpen.SocketOptions.TcpNodelay
		/// 	</seealso>
		public const string TcpNodelay = "http.tcp.nodelay";

		/// <summary>
		/// Determines the size of the internal socket buffer used to buffer data
		/// while receiving / transmitting HTTP messages.
		/// </summary>
		/// <remarks>
		/// Determines the size of the internal socket buffer used to buffer data
		/// while receiving / transmitting HTTP messages.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="int">int</see>
		/// .
		/// </p>
		/// </remarks>
		public const string SocketBufferSize = "http.socket.buffer-size";

		/// <summary>Sets SO_LINGER with the specified linger time in seconds.</summary>
		/// <remarks>
		/// Sets SO_LINGER with the specified linger time in seconds. The maximum
		/// timeout value is platform specific. Value <code>0</code> implies that
		/// the option is disabled. Value <code>-1</code> implies that the JRE
		/// default is used. The setting only affects the socket close operation.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="int">int</see>
		/// .
		/// </p>
		/// </remarks>
		/// <seealso cref="Sharpen.SocketOptions.SoLinger">Sharpen.SocketOptions.SoLinger</seealso>
		public const string SoLinger = "http.socket.linger";

		/// <summary>
		/// Defines whether the socket can be bound even though a previous connection is
		/// still in a timeout state.
		/// </summary>
		/// <remarks>
		/// Defines whether the socket can be bound even though a previous connection is
		/// still in a timeout state.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="bool">bool</see>
		/// .
		/// </p>
		/// </remarks>
		/// <seealso cref="System.Net.Sockets.Socket.SetReuseAddress(bool)">System.Net.Sockets.Socket.SetReuseAddress(bool)
		/// 	</seealso>
		/// <since>4.1</since>
		public const string SoReuseaddr = "http.socket.reuseaddr";

		/// <summary>Determines the timeout in milliseconds until a connection is established.
		/// 	</summary>
		/// <remarks>
		/// Determines the timeout in milliseconds until a connection is established.
		/// A timeout value of zero is interpreted as an infinite timeout.
		/// <p>
		/// Please note this parameter can only be applied to connections that
		/// are bound to a particular local address.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="int">int</see>
		/// .
		/// </p>
		/// </remarks>
		public const string ConnectionTimeout = "http.connection.timeout";

		/// <summary>Determines whether stale connection check is to be used.</summary>
		/// <remarks>
		/// Determines whether stale connection check is to be used. The stale
		/// connection check can cause up to 30 millisecond overhead per request and
		/// should be used only when appropriate. For performance critical
		/// operations this check should be disabled.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="bool">bool</see>
		/// .
		/// </p>
		/// </remarks>
		public const string StaleConnectionCheck = "http.connection.stalecheck";

		/// <summary>Determines the maximum line length limit.</summary>
		/// <remarks>
		/// Determines the maximum line length limit. If set to a positive value,
		/// any HTTP line exceeding this limit will cause an IOException. A negative
		/// or zero value will effectively disable the check.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="int">int</see>
		/// .
		/// </p>
		/// </remarks>
		public const string MaxLineLength = "http.connection.max-line-length";

		/// <summary>Determines the maximum HTTP header count allowed.</summary>
		/// <remarks>
		/// Determines the maximum HTTP header count allowed. If set to a positive
		/// value, the number of HTTP headers received from the data stream exceeding
		/// this limit will cause an IOException. A negative or zero value will
		/// effectively disable the check.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="int">int</see>
		/// .
		/// </p>
		/// </remarks>
		public const string MaxHeaderCount = "http.connection.max-header-count";

		/// <summary>
		/// Defines the size limit below which data chunks should be buffered in a session I/O buffer
		/// in order to minimize native method invocations on the underlying network socket.
		/// </summary>
		/// <remarks>
		/// Defines the size limit below which data chunks should be buffered in a session I/O buffer
		/// in order to minimize native method invocations on the underlying network socket.
		/// The optimal value of this parameter can be platform specific and defines a trade-off
		/// between performance of memory copy operations and that of native method invocation.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="int">int</see>
		/// .
		/// </p>
		/// </remarks>
		/// <since>4.1</since>
		public const string MinChunkLimit = "http.connection.min-chunk-limit";

		/// <summary>
		/// Defines whether or not TCP is to send automatically a keepalive probe to the peer
		/// after an interval of inactivity (no data exchanged in either direction) between this
		/// host and the peer.
		/// </summary>
		/// <remarks>
		/// Defines whether or not TCP is to send automatically a keepalive probe to the peer
		/// after an interval of inactivity (no data exchanged in either direction) between this
		/// host and the peer. The purpose of this option is to detect if the peer host crashes.
		/// <p>
		/// This parameter expects a value of type
		/// <see cref="bool">bool</see>
		/// .
		/// </p>
		/// </remarks>
		/// <seealso cref="Sharpen.SocketOptions.SoKeepalive">Sharpen.SocketOptions.SoKeepalive
		/// 	</seealso>
		/// <since>4.2</since>
		public const string SoKeepalive = "http.socket.keepalive";
	}
}

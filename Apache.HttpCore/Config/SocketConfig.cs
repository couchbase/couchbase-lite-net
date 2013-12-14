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

using System;
using System.Text;
using Org.Apache.Http.Config;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Config
{
	/// <summary>Socket configuration.</summary>
	/// <remarks>Socket configuration.</remarks>
	/// <since>4.3</since>
	public class SocketConfig : ICloneable
	{
		public static readonly Org.Apache.Http.Config.SocketConfig Default = new SocketConfig.Builder
			().Build();

		private readonly int soTimeout;

		private readonly bool soReuseAddress;

		private readonly int soLinger;

		private readonly bool soKeepAlive;

		private readonly bool tcpNoDelay;

		internal SocketConfig(int soTimeout, bool soReuseAddress, int soLinger, bool soKeepAlive
			, bool tcpNoDelay) : base()
		{
			this.soTimeout = soTimeout;
			this.soReuseAddress = soReuseAddress;
			this.soLinger = soLinger;
			this.soKeepAlive = soKeepAlive;
			this.tcpNoDelay = tcpNoDelay;
		}

		/// <summary>Determines the default socket timeout value for non-blocking I/O operations.
		/// 	</summary>
		/// <remarks>
		/// Determines the default socket timeout value for non-blocking I/O operations.
		/// <p/>
		/// Default: <code>0</code> (no timeout)
		/// </remarks>
		/// <seealso cref="Sharpen.SocketOptions.SoTimeout">Sharpen.SocketOptions.SoTimeout</seealso>
		public virtual int GetSoTimeout()
		{
			return soTimeout;
		}

		/// <summary>
		/// Determines the default value of the
		/// <see cref="Sharpen.SocketOptions.SoReuseaddr">Sharpen.SocketOptions.SoReuseaddr</see>
		/// parameter
		/// for newly created sockets.
		/// <p/>
		/// Default: <code>false</code>
		/// </summary>
		/// <seealso cref="Sharpen.SocketOptions.SoReuseaddr">Sharpen.SocketOptions.SoReuseaddr
		/// 	</seealso>
		public virtual bool IsSoReuseAddress()
		{
			return soReuseAddress;
		}

		/// <summary>
		/// Determines the default value of the
		/// <see cref="Sharpen.SocketOptions.SoLinger">Sharpen.SocketOptions.SoLinger</see>
		/// parameter
		/// for newly created sockets.
		/// <p/>
		/// Default: <code>-1</code>
		/// </summary>
		/// <seealso cref="Sharpen.SocketOptions.SoLinger">Sharpen.SocketOptions.SoLinger</seealso>
		public virtual int GetSoLinger()
		{
			return soLinger;
		}

		/// <summary>
		/// Determines the default value of the
		/// <see cref="Sharpen.SocketOptions.SoKeepalive">Sharpen.SocketOptions.SoKeepalive</see>
		/// parameter
		/// for newly created sockets.
		/// <p/>
		/// Default: <code>-1</code>
		/// </summary>
		/// <seealso cref="Sharpen.SocketOptions.SoKeepalive">Sharpen.SocketOptions.SoKeepalive
		/// 	</seealso>
		public virtual bool IsSoKeepAlive()
		{
			return this.soKeepAlive;
		}

		/// <summary>
		/// Determines the default value of the
		/// <see cref="Sharpen.SocketOptions.TcpNodelay">Sharpen.SocketOptions.TcpNodelay</see>
		/// parameter
		/// for newly created sockets.
		/// <p/>
		/// Default: <code>false</code>
		/// </summary>
		/// <seealso cref="Sharpen.SocketOptions.TcpNodelay">Sharpen.SocketOptions.TcpNodelay
		/// 	</seealso>
		public virtual bool IsTcpNoDelay()
		{
			return tcpNoDelay;
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		protected internal virtual Org.Apache.Http.Config.SocketConfig Clone()
		{
			return (Org.Apache.Http.Config.SocketConfig)base.Clone();
		}

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("[soTimeout=").Append(this.soTimeout).Append(", soReuseAddress=").
				Append(this.soReuseAddress).Append(", soLinger=").Append(this.soLinger).Append(", soKeepAlive="
				).Append(this.soKeepAlive).Append(", tcpNoDelay=").Append(this.tcpNoDelay).Append
				("]");
			return builder.ToString();
		}

		public static SocketConfig.Builder Custom()
		{
			return new SocketConfig.Builder();
		}

		public static SocketConfig.Builder Copy(Org.Apache.Http.Config.SocketConfig config
			)
		{
			Args.NotNull(config, "Socket config");
			return new SocketConfig.Builder().SetSoTimeout(config.GetSoTimeout()).SetSoReuseAddress
				(config.IsSoReuseAddress()).SetSoLinger(config.GetSoLinger()).SetSoKeepAlive(config
				.IsSoKeepAlive()).SetTcpNoDelay(config.IsTcpNoDelay());
		}

		public class Builder
		{
			private int soTimeout;

			private bool soReuseAddress;

			private int soLinger;

			private bool soKeepAlive;

			private bool tcpNoDelay;

			internal Builder()
			{
				this.soLinger = -1;
				this.tcpNoDelay = true;
			}

			public virtual SocketConfig.Builder SetSoTimeout(int soTimeout)
			{
				this.soTimeout = soTimeout;
				return this;
			}

			public virtual SocketConfig.Builder SetSoReuseAddress(bool soReuseAddress)
			{
				this.soReuseAddress = soReuseAddress;
				return this;
			}

			public virtual SocketConfig.Builder SetSoLinger(int soLinger)
			{
				this.soLinger = soLinger;
				return this;
			}

			public virtual SocketConfig.Builder SetSoKeepAlive(bool soKeepAlive)
			{
				this.soKeepAlive = soKeepAlive;
				return this;
			}

			public virtual SocketConfig.Builder SetTcpNoDelay(bool tcpNoDelay)
			{
				this.tcpNoDelay = tcpNoDelay;
				return this;
			}

			public virtual SocketConfig Build()
			{
				return new SocketConfig(soTimeout, soReuseAddress, soLinger, soKeepAlive, tcpNoDelay
					);
			}
		}
	}
}

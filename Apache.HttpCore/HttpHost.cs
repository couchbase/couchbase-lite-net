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
using System.Net;
using System.Text;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>Holds all of the variables needed to describe an HTTP connection to a host.
	/// 	</summary>
	/// <remarks>
	/// Holds all of the variables needed to describe an HTTP connection to a host.
	/// This includes remote host name, port and scheme.
	/// </remarks>
	/// <since>4.0</since>
	[System.Serializable]
	public sealed class HttpHost : ICloneable
	{
		private const long serialVersionUID = -7529410654042457626L;

		/// <summary>The default scheme is "http".</summary>
		/// <remarks>The default scheme is "http".</remarks>
		public const string DefaultSchemeName = "http";

		/// <summary>The host to use.</summary>
		/// <remarks>The host to use.</remarks>
		protected internal readonly string hostname;

		/// <summary>
		/// The lowercase host, for
		/// <see cref="Equals(object)">Equals(object)</see>
		/// and
		/// <see cref="GetHashCode()">GetHashCode()</see>
		/// .
		/// </summary>
		protected internal readonly string lcHostname;

		/// <summary>The port to use, defaults to -1 if not set.</summary>
		/// <remarks>The port to use, defaults to -1 if not set.</remarks>
		protected internal readonly int port;

		/// <summary>The scheme (lowercased)</summary>
		protected internal readonly string schemeName;

		protected internal readonly IPAddress address;

		/// <summary>
		/// Creates a new
		/// <see cref="HttpHost">HttpHost</see>
		/// , specifying all values.
		/// Constructor for HttpHost.
		/// </summary>
		/// <param name="hostname">the hostname (IP or DNS name)</param>
		/// <param name="port">
		/// the port number.
		/// <code>-1</code> indicates the scheme default port.
		/// </param>
		/// <param name="scheme">
		/// the name of the scheme.
		/// <code>null</code> indicates the
		/// <see cref="DefaultSchemeName">default scheme</see>
		/// </param>
		public HttpHost(string hostname, int port, string scheme) : base()
		{
            this.hostname = Args.NotBlank(((CharSequence)hostname), "Host name").ToString();
			this.lcHostname = hostname.ToLower(Sharpen.Extensions.GetEnglishCulture());
			if (scheme != null)
			{
				this.schemeName = scheme.ToLower(Sharpen.Extensions.GetEnglishCulture());
			}
			else
			{
				this.schemeName = DefaultSchemeName;
			}
			this.port = port;
			this.address = null;
		}

		/// <summary>
		/// Creates a new
		/// <see cref="HttpHost">HttpHost</see>
		/// , with default scheme.
		/// </summary>
		/// <param name="hostname">the hostname (IP or DNS name)</param>
		/// <param name="port">
		/// the port number.
		/// <code>-1</code> indicates the scheme default port.
		/// </param>
		public HttpHost(string hostname, int port) : this(hostname, port, null)
		{
		}

		/// <summary>
		/// Creates a new
		/// <see cref="HttpHost">HttpHost</see>
		/// , with default scheme and port.
		/// </summary>
		/// <param name="hostname">the hostname (IP or DNS name)</param>
		public HttpHost(string hostname) : this(hostname, -1, null)
		{
		}

		/// <summary>
		/// Creates a new
		/// <see cref="HttpHost">HttpHost</see>
		/// , specifying all values.
		/// Constructor for HttpHost.
		/// </summary>
		/// <param name="address">the inet address.</param>
		/// <param name="port">
		/// the port number.
		/// <code>-1</code> indicates the scheme default port.
		/// </param>
		/// <param name="scheme">
		/// the name of the scheme.
		/// <code>null</code> indicates the
		/// <see cref="DefaultSchemeName">default scheme</see>
		/// </param>
		/// <since>4.3</since>
		public HttpHost(IPAddress address, int port, string scheme) : base()
		{
			this.address = Args.NotNull(address, "Inet address");
			this.hostname = address.GetHostAddress();
			this.lcHostname = this.hostname.ToLower(Sharpen.Extensions.GetEnglishCulture());
			if (scheme != null)
			{
				this.schemeName = scheme.ToLower(Sharpen.Extensions.GetEnglishCulture());
			}
			else
			{
				this.schemeName = DefaultSchemeName;
			}
			this.port = port;
		}

		/// <summary>
		/// Creates a new
		/// <see cref="HttpHost">HttpHost</see>
		/// , with default scheme.
		/// </summary>
		/// <param name="address">the inet address.</param>
		/// <param name="port">
		/// the port number.
		/// <code>-1</code> indicates the scheme default port.
		/// </param>
		/// <since>4.3</since>
		public HttpHost(IPAddress address, int port) : this(address, port, null)
		{
		}

		/// <summary>
		/// Creates a new
		/// <see cref="HttpHost">HttpHost</see>
		/// , with default scheme and port.
		/// </summary>
		/// <param name="address">the inet address.</param>
		/// <since>4.3</since>
		public HttpHost(IPAddress address) : this(address, -1, null)
		{
		}

		/// <summary>
		/// Copy constructor for
		/// <see cref="HttpHost">HttpHost</see>
		/// .
		/// </summary>
		/// <param name="httphost">the HTTP host to copy details from</param>
		public HttpHost(Org.Apache.Http.HttpHost httphost) : base()
		{
			Args.NotNull(httphost, "HTTP host");
			this.hostname = httphost.hostname;
			this.lcHostname = httphost.lcHostname;
			this.schemeName = httphost.schemeName;
			this.port = httphost.port;
			this.address = httphost.address;
		}

		/// <summary>Returns the host name.</summary>
		/// <remarks>Returns the host name.</remarks>
		/// <returns>the host name (IP or DNS name)</returns>
		public string GetHostName()
		{
			return this.hostname;
		}

		/// <summary>Returns the port.</summary>
		/// <remarks>Returns the port.</remarks>
		/// <returns>the host port, or <code>-1</code> if not set</returns>
		public int GetPort()
		{
			return this.port;
		}

		/// <summary>Returns the scheme name.</summary>
		/// <remarks>Returns the scheme name.</remarks>
		/// <returns>the scheme name</returns>
		public string GetSchemeName()
		{
			return this.schemeName;
		}

		/// <summary>
		/// Returns the inet address if explicitly set by a constructor,
		/// <code>null</code> otherwise.
		/// </summary>
		/// <remarks>
		/// Returns the inet address if explicitly set by a constructor,
		/// <code>null</code> otherwise.
		/// </remarks>
		/// <returns>the inet address</returns>
		/// <since>4.3</since>
		public IPAddress GetAddress()
		{
			return this.address;
		}

		/// <summary>Return the host URI, as a string.</summary>
		/// <remarks>Return the host URI, as a string.</remarks>
		/// <returns>the host URI</returns>
		public string ToURI()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append(this.schemeName);
			buffer.Append("://");
			buffer.Append(this.hostname);
			if (this.port != -1)
			{
				buffer.Append(':');
				buffer.Append(Sharpen.Extensions.ToString(this.port));
			}
			return buffer.ToString();
		}

		/// <summary>Obtains the host string, without scheme prefix.</summary>
		/// <remarks>Obtains the host string, without scheme prefix.</remarks>
		/// <returns>the host string, for example <code>localhost:8080</code></returns>
		public string ToHostString()
		{
			if (this.port != -1)
			{
				//the highest port number is 65535, which is length 6 with the addition of the colon
				StringBuilder buffer = new StringBuilder(this.hostname.Length + 6);
				buffer.Append(this.hostname);
				buffer.Append(":");
				buffer.Append(Sharpen.Extensions.ToString(this.port));
				return buffer.ToString();
			}
			else
			{
				return this.hostname;
			}
		}

		public override string ToString()
		{
			return ToURI();
		}

		public override bool Equals(object obj)
		{
			if (this == obj)
			{
				return true;
			}
			if (obj is Org.Apache.Http.HttpHost)
			{
				Org.Apache.Http.HttpHost that = (Org.Apache.Http.HttpHost)obj;
				return this.lcHostname.Equals(that.lcHostname) && this.port == that.port && this.
					schemeName.Equals(that.schemeName);
			}
			else
			{
				return false;
			}
		}

		/// <seealso cref="object.GetHashCode()">object.GetHashCode()</seealso>
		public override int GetHashCode()
		{
			int hash = LangUtils.HashSeed;
			hash = LangUtils.HashCode(hash, this.lcHostname);
			hash = LangUtils.HashCode(hash, this.port);
			hash = LangUtils.HashCode(hash, this.schemeName);
			return hash;
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public object Clone()
		{
            return base.MemberwiseClone();
		}
	}
}

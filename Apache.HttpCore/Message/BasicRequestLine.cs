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
using Org.Apache.Http;
using Org.Apache.Http.Message;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>
	/// Basic implementation of
	/// <see cref="Org.Apache.Http.RequestLine">Org.Apache.Http.RequestLine</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	[System.Serializable]
	public class BasicRequestLine : RequestLine, ICloneable
	{
		private const long serialVersionUID = 2810581718468737193L;

		private readonly ProtocolVersion protoversion;

		private readonly string method;

		private readonly string uri;

		public BasicRequestLine(string method, string uri, ProtocolVersion version) : base
			()
		{
			this.method = Args.NotNull(method, "Method");
			this.uri = Args.NotNull(uri, "URI");
			this.protoversion = Args.NotNull(version, "Version");
		}

		public virtual string GetMethod()
		{
			return this.method;
		}

		public virtual ProtocolVersion GetProtocolVersion()
		{
			return this.protoversion;
		}

		public virtual string GetUri()
		{
			return this.uri;
		}

		public override string ToString()
		{
			// no need for non-default formatting in toString()
			return BasicLineFormatter.Instance.FormatRequestLine(null, this).ToString();
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			return base.MemberwiseClone();
		}
	}
}

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
	/// <see cref="Org.Apache.Http.StatusLine">Org.Apache.Http.StatusLine</see>
	/// </summary>
	/// <since>4.0</since>
	[System.Serializable]
	public class BasicStatusLine : StatusLine, ICloneable
	{
		private const long serialVersionUID = -2443303766890459269L;

		/// <summary>The protocol version.</summary>
		/// <remarks>The protocol version.</remarks>
		private readonly ProtocolVersion protoVersion;

		/// <summary>The status code.</summary>
		/// <remarks>The status code.</remarks>
		private readonly int statusCode;

		/// <summary>The reason phrase.</summary>
		/// <remarks>The reason phrase.</remarks>
		private readonly string reasonPhrase;

		/// <summary>Creates a new status line with the given version, status, and reason.</summary>
		/// <remarks>Creates a new status line with the given version, status, and reason.</remarks>
		/// <param name="version">the protocol version of the response</param>
		/// <param name="statusCode">the status code of the response</param>
		/// <param name="reasonPhrase">
		/// the reason phrase to the status code, or
		/// <code>null</code>
		/// </param>
		public BasicStatusLine(ProtocolVersion version, int statusCode, string reasonPhrase
			) : base()
		{
			// ----------------------------------------------------- Instance Variables
			// ----------------------------------------------------------- Constructors
			this.protoVersion = Args.NotNull(version, "Version");
			this.statusCode = Args.NotNegative(statusCode, "Status code");
			this.reasonPhrase = reasonPhrase;
		}

		// --------------------------------------------------------- Public Methods
		public virtual int GetStatusCode()
		{
			return this.statusCode;
		}

		public virtual ProtocolVersion GetProtocolVersion()
		{
			return this.protoVersion;
		}

		public virtual string GetReasonPhrase()
		{
			return this.reasonPhrase;
		}

		public override string ToString()
		{
			// no need for non-default formatting in toString()
			return BasicLineFormatter.Instance.FormatStatusLine(null, this).ToString();
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			return base.MemberwiseClone();
		}
	}
}

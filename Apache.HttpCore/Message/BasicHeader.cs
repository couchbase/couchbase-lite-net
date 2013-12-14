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
	/// <see cref="Org.Apache.Http.Header">Org.Apache.Http.Header</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	[System.Serializable]
	public class BasicHeader : Header, ICloneable
	{
		private const long serialVersionUID = -5427236326487562174L;

		private readonly string name;

		private readonly string value;

		/// <summary>Constructor with name and value</summary>
		/// <param name="name">the header name</param>
		/// <param name="value">the header value</param>
		public BasicHeader(string name, string value) : base()
		{
			this.name = Args.NotNull(name, "Name");
			this.value = value;
		}

		public virtual string GetName()
		{
			return this.name;
		}

		public virtual string GetValue()
		{
			return this.value;
		}

		public override string ToString()
		{
			// no need for non-default formatting in toString()
			return BasicLineFormatter.Instance.FormatHeader(null, this).ToString();
		}

		/// <exception cref="Org.Apache.Http.ParseException"></exception>
		public virtual HeaderElement[] GetElements()
		{
			if (this.value != null)
			{
				// result intentionally not cached, it's probably not used again
				return BasicHeaderValueParser.ParseElements(this.value, null);
			}
			else
			{
				return new HeaderElement[] {  };
			}
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			return base.MemberwiseClone();
		}
	}
}

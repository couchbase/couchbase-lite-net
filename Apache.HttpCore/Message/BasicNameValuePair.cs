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
using Org.Apache.Http;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>
	/// Basic implementation of
	/// <see cref="Org.Apache.Http.NameValuePair">Org.Apache.Http.NameValuePair</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	[System.Serializable]
	public class BasicNameValuePair : NameValuePair, ICloneable
	{
		private const long serialVersionUID = -6437800749411518984L;

		private readonly string name;

		private readonly string value;

		/// <summary>Default Constructor taking a name and a value.</summary>
		/// <remarks>Default Constructor taking a name and a value. The value may be null.</remarks>
		/// <param name="name">The name.</param>
		/// <param name="value">The value.</param>
		public BasicNameValuePair(string name, string value) : base()
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
			// don't call complex default formatting for a simple toString
			if (this.value == null)
			{
				return name;
			}
			int len = this.name.Length + 1 + this.value.Length;
			StringBuilder buffer = new StringBuilder(len);
			buffer.Append(this.name);
			buffer.Append("=");
			buffer.Append(this.value);
			return buffer.ToString();
		}

		public override bool Equals(object @object)
		{
			if (this == @object)
			{
				return true;
			}
			if (@object is NameValuePair)
			{
				Org.Apache.Http.Message.BasicNameValuePair that = (Org.Apache.Http.Message.BasicNameValuePair
					)@object;
				return this.name.Equals(that.name) && LangUtils.Equals(this.value, that.value);
			}
			return false;
		}

		public override int GetHashCode()
		{
			int hash = LangUtils.HashSeed;
			hash = LangUtils.HashCode(hash, this.name);
			hash = LangUtils.HashCode(hash, this.value);
			return hash;
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			return base.MemberwiseClone();
		}
	}
}

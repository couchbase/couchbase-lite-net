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
	/// <see cref="Org.Apache.Http.HeaderElement">Org.Apache.Http.HeaderElement</see>
	/// </summary>
	/// <since>4.0</since>
	public class BasicHeaderElement : HeaderElement, ICloneable
	{
		private readonly string name;

		private readonly string value;

		private readonly NameValuePair[] parameters;

		/// <summary>Constructor with name, value and parameters.</summary>
		/// <remarks>Constructor with name, value and parameters.</remarks>
		/// <param name="name">header element name</param>
		/// <param name="value">header element value. May be <tt>null</tt></param>
		/// <param name="parameters">
		/// header element parameters. May be <tt>null</tt>.
		/// Parameters are copied by reference, not by value
		/// </param>
		public BasicHeaderElement(string name, string value, NameValuePair[] parameters) : 
			base()
		{
			this.name = Args.NotNull(name, "Name");
			this.value = value;
			if (parameters != null)
			{
				this.parameters = parameters;
			}
			else
			{
				this.parameters = new NameValuePair[] {  };
			}
		}

		/// <summary>Constructor with name and value.</summary>
		/// <remarks>Constructor with name and value.</remarks>
		/// <param name="name">header element name</param>
		/// <param name="value">header element value. May be <tt>null</tt></param>
		public BasicHeaderElement(string name, string value) : this(name, value, null)
		{
		}

		public virtual string GetName()
		{
			return this.name;
		}

		public virtual string GetValue()
		{
			return this.value;
		}

		public virtual NameValuePair[] GetParameters()
		{
			return this.parameters.Clone();
		}

		public virtual int GetParameterCount()
		{
			return this.parameters.Length;
		}

		public virtual NameValuePair GetParameter(int index)
		{
			// ArrayIndexOutOfBoundsException is appropriate
			return this.parameters[index];
		}

		public virtual NameValuePair GetParameterByName(string name)
		{
			Args.NotNull(name, "Name");
			NameValuePair found = null;
			foreach (NameValuePair current in this.parameters)
			{
				if (Sharpen.Runtime.EqualsIgnoreCase(current.GetName(), name))
				{
					found = current;
					break;
				}
			}
			return found;
		}

		public override bool Equals(object @object)
		{
			if (this == @object)
			{
				return true;
			}
			if (@object is HeaderElement)
			{
				Org.Apache.Http.Message.BasicHeaderElement that = (Org.Apache.Http.Message.BasicHeaderElement
					)@object;
				return this.name.Equals(that.name) && LangUtils.Equals(this.value, that.value) &&
					 LangUtils.Equals(this.parameters, that.parameters);
			}
			else
			{
				return false;
			}
		}

		public override int GetHashCode()
		{
			int hash = LangUtils.HashSeed;
			hash = LangUtils.HashCode(hash, this.name);
			hash = LangUtils.HashCode(hash, this.value);
			foreach (NameValuePair parameter in this.parameters)
			{
				hash = LangUtils.HashCode(hash, parameter);
			}
			return hash;
		}

		public override string ToString()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append(this.name);
			if (this.value != null)
			{
				buffer.Append("=");
				buffer.Append(this.value);
			}
			foreach (NameValuePair parameter in this.parameters)
			{
				buffer.Append("; ");
				buffer.Append(parameter);
			}
			return buffer.ToString();
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			// parameters array is considered immutable
			// no need to make a copy of it
			return base.MemberwiseClone();
		}
	}
}

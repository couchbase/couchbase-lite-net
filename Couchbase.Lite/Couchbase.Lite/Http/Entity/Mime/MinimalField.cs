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

using System.Text;
using Sharpen;

namespace Org.Apache.Http.Entity.Mime
{
	/// <summary>Minimal MIME field.</summary>
	/// <remarks>Minimal MIME field.</remarks>
	/// <since>4.0</since>
	public class MinimalField
	{
		private readonly string name;

		private readonly string value;

		internal MinimalField(string name, string value) : base()
		{
			this.name = name;
			this.value = value;
		}

		public virtual string GetName()
		{
			return this.name;
		}

		public virtual string GetBody()
		{
			return this.value;
		}

		public override string ToString()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append(this.name);
			buffer.Append(": ");
			buffer.Append(this.value);
			return buffer.ToString();
		}
	}
}

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

using System.Collections.Generic;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>
	/// Default implementation of
	/// <see cref="HttpContext">HttpContext</see>
	/// .
	/// <p>
	/// Please note instances of this class can be thread unsafe if the
	/// parent context is not thread safe.
	/// </summary>
	/// <since>4.0</since>
	public class BasicHttpContext : HttpContext
	{
		private readonly HttpContext parentContext;

		private readonly IDictionary<string, object> map;

		public BasicHttpContext() : this(null)
		{
		}

		public BasicHttpContext(HttpContext parentContext) : base()
		{
			this.map = new ConcurrentHashMap<string, object>();
			this.parentContext = parentContext;
		}

		public override object GetAttribute(string id)
		{
			Args.NotNull(id, "Id");
			object obj = this.map.Get(id);
			if (obj == null && this.parentContext != null)
			{
				obj = this.parentContext.GetAttribute(id);
			}
			return obj;
		}

		public override void SetAttribute(string id, object obj)
		{
			Args.NotNull(id, "Id");
			if (obj != null)
			{
				this.map.Put(id, obj);
			}
			else
			{
				Sharpen.Collections.Remove(this.map, id);
			}
		}

		public override object RemoveAttribute(string id)
		{
			Args.NotNull(id, "Id");
			return Sharpen.Collections.Remove(this.map, id);
		}

		/// <since>4.2</since>
		public virtual void Clear()
		{
			this.map.Clear();
		}

		public override string ToString()
		{
			return this.map.ToString();
		}
	}
}

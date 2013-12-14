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
using Org.Apache.Http.Params;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>
	/// Basic implementation of
	/// <see cref="Org.Apache.Http.HttpMessage">Org.Apache.Http.HttpMessage</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	public abstract class AbstractHttpMessage : HttpMessage
	{
		protected internal HeaderGroup headergroup;

		[Obsolete]
		protected internal HttpParams @params;

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) use AbstractHttpMessage()")]
		protected internal AbstractHttpMessage(HttpParams @params) : base()
		{
			this.headergroup = new HeaderGroup();
			this.@params = @params;
		}

		protected internal AbstractHttpMessage() : this(null)
		{
		}

		// non-javadoc, see interface HttpMessage
		public virtual bool ContainsHeader(string name)
		{
			return this.headergroup.ContainsHeader(name);
		}

		// non-javadoc, see interface HttpMessage
		public virtual Header[] GetHeaders(string name)
		{
			return this.headergroup.GetHeaders(name);
		}

		// non-javadoc, see interface HttpMessage
		public virtual Header GetFirstHeader(string name)
		{
			return this.headergroup.GetFirstHeader(name);
		}

		// non-javadoc, see interface HttpMessage
		public virtual Header GetLastHeader(string name)
		{
			return this.headergroup.GetLastHeader(name);
		}

		// non-javadoc, see interface HttpMessage
		public virtual Header[] GetAllHeaders()
		{
			return this.headergroup.GetAllHeaders();
		}

		// non-javadoc, see interface HttpMessage
		public virtual void AddHeader(Header header)
		{
			this.headergroup.AddHeader(header);
		}

		// non-javadoc, see interface HttpMessage
		public virtual void AddHeader(string name, string value)
		{
			Args.NotNull(name, "Header name");
			this.headergroup.AddHeader(new BasicHeader(name, value));
		}

		// non-javadoc, see interface HttpMessage
		public virtual void SetHeader(Header header)
		{
			this.headergroup.UpdateHeader(header);
		}

		// non-javadoc, see interface HttpMessage
		public virtual void SetHeader(string name, string value)
		{
			Args.NotNull(name, "Header name");
			this.headergroup.UpdateHeader(new BasicHeader(name, value));
		}

		// non-javadoc, see interface HttpMessage
		public virtual void SetHeaders(Header[] headers)
		{
			this.headergroup.SetHeaders(headers);
		}

		// non-javadoc, see interface HttpMessage
		public virtual void RemoveHeader(Header header)
		{
			this.headergroup.RemoveHeader(header);
		}

		// non-javadoc, see interface HttpMessage
		public virtual void RemoveHeaders(string name)
		{
			if (name == null)
			{
				return;
			}
			for (Org.Apache.Http.HeaderIterator i = this.headergroup.Iterator(); i.HasNext(); )
			{
				Header header = i.NextHeader();
				if (Sharpen.Runtime.EqualsIgnoreCase(name, header.GetName()))
				{
					i.Remove();
				}
			}
		}

		// non-javadoc, see interface HttpMessage
		public virtual Org.Apache.Http.HeaderIterator HeaderIterator()
		{
			return this.headergroup.Iterator();
		}

		// non-javadoc, see interface HttpMessage
		public virtual Org.Apache.Http.HeaderIterator HeaderIterator(string name)
		{
			return this.headergroup.Iterator(name);
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) use constructor parameters of configuration API provided by HttpClient"
			)]
		public virtual HttpParams GetParams()
		{
			if (this.@params == null)
			{
				this.@params = new BasicHttpParams();
			}
			return this.@params;
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) use constructor parameters of configuration API provided by HttpClient"
			)]
		public virtual void SetParams(HttpParams @params)
		{
			this.@params = Args.NotNull(@params, "HTTP parameters");
		}

		public abstract ProtocolVersion GetProtocolVersion();
	}
}

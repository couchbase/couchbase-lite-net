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

using Org.Apache.Http;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>
	/// Implementation of
	/// <see cref="HttpContext">HttpContext</see>
	/// that provides convenience
	/// setters for user assignable attributes and getter for readable attributes.
	/// </summary>
	/// <since>4.3</since>
	public class HttpCoreContext : HttpContext
	{
		/// <summary>
		/// Attribute name of a
		/// <see cref="Org.Apache.Http.HttpConnection">Org.Apache.Http.HttpConnection</see>
		/// object that
		/// represents the actual HTTP connection.
		/// </summary>
		public const string HttpConnection = "http.connection";

		/// <summary>
		/// Attribute name of a
		/// <see cref="Org.Apache.Http.IHttpRequest">Org.Apache.Http.IHttpRequest</see>
		/// object that
		/// represents the actual HTTP request.
		/// </summary>
		public const string HttpRequest = "http.request";

		/// <summary>
		/// Attribute name of a
		/// <see cref="Org.Apache.Http.HttpResponse">Org.Apache.Http.HttpResponse</see>
		/// object that
		/// represents the actual HTTP response.
		/// </summary>
		public const string HttpResponse = "http.response";

		/// <summary>
		/// Attribute name of a
		/// <see cref="Org.Apache.Http.HttpHost">Org.Apache.Http.HttpHost</see>
		/// object that
		/// represents the connection target.
		/// </summary>
		public const string HttpTargetHost = "http.target_host";

		/// <summary>
		/// Attribute name of a
		/// <see cref="bool">bool</see>
		/// object that represents the
		/// the flag indicating whether the actual request has been fully transmitted
		/// to the target host.
		/// </summary>
		public const string HttpReqSent = "http.request_sent";

		public static Org.Apache.Http.Protocol.HttpCoreContext Create()
		{
			return new Org.Apache.Http.Protocol.HttpCoreContext(new BasicHttpContext());
		}

		public static Org.Apache.Http.Protocol.HttpCoreContext Adapt(HttpContext context)
		{
			Args.NotNull(context, "HTTP context");
			if (context is Org.Apache.Http.Protocol.HttpCoreContext)
			{
				return (Org.Apache.Http.Protocol.HttpCoreContext)context;
			}
			else
			{
				return new Org.Apache.Http.Protocol.HttpCoreContext(context);
			}
		}

		private readonly HttpContext context;

		public HttpCoreContext(HttpContext context) : base()
		{
			this.context = context;
		}

		public HttpCoreContext() : base()
		{
			this.context = new BasicHttpContext();
		}

		public override object GetAttribute(string id)
		{
			return context.GetAttribute(id);
		}

		public override void SetAttribute(string id, object obj)
		{
			context.SetAttribute(id, obj);
		}

		public override object RemoveAttribute(string id)
		{
			return context.RemoveAttribute(id);
		}

		public virtual T GetAttribute<T>(string attribname)
		{
			System.Type clazz = typeof(T);
			Args.NotNull(clazz, "Attribute class");
			object obj = GetAttribute(attribname);
			if (obj == null)
			{
				return null;
			}
			return clazz.Cast(obj);
		}

		public virtual T GetConnection<T>() where T:HttpConnection
		{
			System.Type clazz = typeof(T);
			return GetAttribute(HttpConnection, clazz);
		}

		public virtual HttpConnection GetConnection()
		{
			return GetAttribute<HttpConnection>(HttpConnection);
		}

		public virtual IHttpRequest GetRequest()
		{
			return GetAttribute<IHttpRequest>(HttpRequest);
		}

		public virtual bool IsRequestSent()
		{
			bool b = GetAttribute<bool>(HttpReqSent);
			return b != null && b;
		}

		public virtual HttpResponse GetResponse()
		{
			return GetAttribute<HttpResponse>(HttpResponse);
		}

		public virtual void SetTargetHost(HttpHost host)
		{
			SetAttribute(HttpTargetHost, host);
		}

		public virtual HttpHost GetTargetHost()
		{
			return GetAttribute<HttpHost>(HttpTargetHost);
		}
	}
}

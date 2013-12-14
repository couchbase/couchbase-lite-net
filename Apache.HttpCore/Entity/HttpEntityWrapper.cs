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
using System.IO;
using Org.Apache.Http;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Entity
{
	/// <summary>Base class for wrapping entities.</summary>
	/// <remarks>
	/// Base class for wrapping entities.
	/// Keeps a
	/// <see cref="wrappedEntity">wrappedEntity</see>
	/// and delegates all
	/// calls to it. Implementations of wrapping entities can derive
	/// from this class and need to override only those methods that
	/// should not be delegated to the wrapped entity.
	/// </remarks>
	/// <since>4.0</since>
	public class HttpEntityWrapper : HttpEntity
	{
		/// <summary>The wrapped entity.</summary>
		/// <remarks>The wrapped entity.</remarks>
		protected internal HttpEntity wrappedEntity;

		/// <summary>Creates a new entity wrapper.</summary>
		/// <remarks>Creates a new entity wrapper.</remarks>
		public HttpEntityWrapper(HttpEntity wrappedEntity) : base()
		{
			this.wrappedEntity = Args.NotNull(wrappedEntity, "Wrapped entity");
		}

		// constructor
		public virtual bool IsRepeatable()
		{
			return wrappedEntity.IsRepeatable();
		}

		public virtual bool IsChunked()
		{
			return wrappedEntity.IsChunked();
		}

		public virtual long GetContentLength()
		{
			return wrappedEntity.GetContentLength();
		}

		public virtual Header GetContentType()
		{
			return wrappedEntity.GetContentType();
		}

		public virtual Header GetContentEncoding()
		{
			return wrappedEntity.GetContentEncoding();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual InputStream GetContent()
		{
			return wrappedEntity.GetContent();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void WriteTo(OutputStream outstream)
		{
			wrappedEntity.WriteTo(outstream);
		}

		public virtual bool IsStreaming()
		{
			return wrappedEntity.IsStreaming();
		}

		/// <exception cref="System.IO.IOException"></exception>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) Either use GetContent() and call System.IO.InputStream.Close() on that; otherwise call WriteTo(System.IO.OutputStream) which is required to free the resources."
			)]
		public virtual void ConsumeContent()
		{
			wrappedEntity.ConsumeContent();
		}
	}
}

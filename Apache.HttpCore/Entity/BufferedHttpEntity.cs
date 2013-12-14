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

using System.IO;
using Org.Apache.Http;
using Org.Apache.Http.Entity;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Entity
{
	/// <summary>A wrapping entity that buffers it content if necessary.</summary>
	/// <remarks>
	/// A wrapping entity that buffers it content if necessary.
	/// The buffered entity is always repeatable.
	/// If the wrapped entity is repeatable itself, calls are passed through.
	/// If the wrapped entity is not repeatable, the content is read into a
	/// buffer once and provided from there as often as required.
	/// </remarks>
	/// <since>4.0</since>
	public class BufferedHttpEntity : HttpEntityWrapper
	{
		private readonly byte[] buffer;

		/// <summary>Creates a new buffered entity wrapper.</summary>
		/// <remarks>Creates a new buffered entity wrapper.</remarks>
		/// <param name="entity">the entity to wrap, not null</param>
		/// <exception cref="System.ArgumentException">if wrapped is null</exception>
		/// <exception cref="System.IO.IOException"></exception>
		public BufferedHttpEntity(HttpEntity entity) : base(entity)
		{
			if (!entity.IsRepeatable() || entity.GetContentLength() < 0)
			{
				this.buffer = EntityUtils.ToByteArray(entity);
			}
			else
			{
				this.buffer = null;
			}
		}

		public override long GetContentLength()
		{
			if (this.buffer != null)
			{
				return this.buffer.Length;
			}
			else
			{
				return base.GetContentLength();
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override InputStream GetContent()
		{
			if (this.buffer != null)
			{
				return new ByteArrayInputStream(this.buffer);
			}
			else
			{
				return base.GetContent();
			}
		}

		/// <summary>Tells that this entity does not have to be chunked.</summary>
		/// <remarks>Tells that this entity does not have to be chunked.</remarks>
		/// <returns><code>false</code></returns>
		public override bool IsChunked()
		{
			return (buffer == null) && base.IsChunked();
		}

		/// <summary>Tells that this entity is repeatable.</summary>
		/// <remarks>Tells that this entity is repeatable.</remarks>
		/// <returns><code>true</code></returns>
		public override bool IsRepeatable()
		{
			return true;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void WriteTo(OutputStream outstream)
		{
			Args.NotNull(outstream, "Output stream");
			if (this.buffer != null)
			{
				outstream.Write(this.buffer);
			}
			else
			{
				base.WriteTo(outstream);
			}
		}

		// non-javadoc, see interface HttpEntity
		public override bool IsStreaming()
		{
			return (buffer == null) && base.IsStreaming();
		}
	}
}

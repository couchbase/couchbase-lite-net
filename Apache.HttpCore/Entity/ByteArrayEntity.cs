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
using Org.Apache.Http.Entity;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Entity
{
	/// <summary>A self contained, repeatable entity that obtains its content from a byte array.
	/// 	</summary>
	/// <remarks>A self contained, repeatable entity that obtains its content from a byte array.
	/// 	</remarks>
	/// <since>4.0</since>
	public class ByteArrayEntity : AbstractHttpEntity, ICloneable
	{
		[System.ObsoleteAttribute(@"(4.2)")]
		[Obsolete]
		protected internal readonly byte[] content;

		private readonly byte[] b;

		private readonly int off;

		private readonly int len;

		/// <since>4.2</since>
		public ByteArrayEntity(byte[] b, ContentType contentType) : base()
		{
			Args.NotNull(b, "Source byte array");
			this.content = b;
			this.b = b;
			this.off = 0;
			this.len = this.b.Length;
			if (contentType != null)
			{
				SetContentType(contentType.ToString());
			}
		}

		/// <since>4.2</since>
		public ByteArrayEntity(byte[] b, int off, int len, ContentType contentType) : base
			()
		{
			Args.NotNull(b, "Source byte array");
			if ((off < 0) || (off > b.Length) || (len < 0) || ((off + len) < 0) || ((off + len
				) > b.Length))
			{
				throw new IndexOutOfRangeException("off: " + off + " len: " + len + " b.length: "
					 + b.Length);
			}
			this.content = b;
			this.b = b;
			this.off = off;
			this.len = len;
			if (contentType != null)
			{
				SetContentType(contentType.ToString());
			}
		}

		public ByteArrayEntity(byte[] b) : this(b, null)
		{
		}

		public ByteArrayEntity(byte[] b, int off, int len) : this(b, off, len, null)
		{
		}

		public override bool IsRepeatable()
		{
			return true;
		}

		public override long GetContentLength()
		{
			return this.len;
		}

		public override InputStream GetContent()
		{
			return new ByteArrayInputStream(this.b, this.off, this.len);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void WriteTo(OutputStream outstream)
		{
			Args.NotNull(outstream, "Output stream");
			outstream.Write(this.b, this.off, this.len);
			outstream.Flush();
		}

		/// <summary>Tells that this entity is not streaming.</summary>
		/// <remarks>Tells that this entity is not streaming.</remarks>
		/// <returns><code>false</code></returns>
		public override bool IsStreaming()
		{
			return false;
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			return base.MemberwiseClone();
		}
	}
}

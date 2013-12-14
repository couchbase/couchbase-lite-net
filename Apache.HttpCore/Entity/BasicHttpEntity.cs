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
using Org.Apache.Http.Entity;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Entity
{
	/// <summary>
	/// A generic streamed, non-repeatable entity that obtains its content
	/// from an
	/// <see cref="System.IO.InputStream">System.IO.InputStream</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	public class BasicHttpEntity : AbstractHttpEntity
	{
		private InputStream content;

		private long length;

		/// <summary>Creates a new basic entity.</summary>
		/// <remarks>
		/// Creates a new basic entity.
		/// The content is initially missing, the content length
		/// is set to a negative number.
		/// </remarks>
		public BasicHttpEntity() : base()
		{
			this.length = -1;
		}

		public override long GetContentLength()
		{
			return this.length;
		}

		/// <summary>Obtains the content, once only.</summary>
		/// <remarks>Obtains the content, once only.</remarks>
		/// <returns>
		/// the content, if this is the first call to this method
		/// since
		/// <see cref="SetContent(System.IO.InputStream)">setContent</see>
		/// has been called
		/// </returns>
		/// <exception cref="System.InvalidOperationException">if the content has not been provided
		/// 	</exception>
		public override InputStream GetContent()
		{
			Asserts.Check(this.content != null, "Content has not been provided");
			return this.content;
		}

		/// <summary>Tells that this entity is not repeatable.</summary>
		/// <remarks>Tells that this entity is not repeatable.</remarks>
		/// <returns><code>false</code></returns>
		public override bool IsRepeatable()
		{
			return false;
		}

		/// <summary>Specifies the length of the content.</summary>
		/// <remarks>Specifies the length of the content.</remarks>
		/// <param name="len">
		/// the number of bytes in the content, or
		/// a negative number to indicate an unknown length
		/// </param>
		public virtual void SetContentLength(long len)
		{
			this.length = len;
		}

		/// <summary>Specifies the content.</summary>
		/// <remarks>Specifies the content.</remarks>
		/// <param name="instream">
		/// the stream to return with the next call to
		/// <see cref="GetContent()">getContent</see>
		/// </param>
		public virtual void SetContent(InputStream instream)
		{
			this.content = instream;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void WriteTo(OutputStream outstream)
		{
			Args.NotNull(outstream, "Output stream");
			InputStream instream = GetContent();
			try
			{
				int l;
				byte[] tmp = new byte[OutputBufferSize];
				while ((l = instream.Read(tmp)) != -1)
				{
					outstream.Write(tmp, 0, l);
				}
			}
			finally
			{
				instream.Close();
			}
		}

		public override bool IsStreaming()
		{
			return this.content != null;
		}
	}
}

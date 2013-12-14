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
using Org.Apache.Http.IO;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Impl.IO
{
	/// <summary>Output stream that writes data without any transformation.</summary>
	/// <remarks>
	/// Output stream that writes data without any transformation. The end of
	/// the content entity is demarcated by closing the underlying connection
	/// (EOF condition). Entities transferred using this input stream can be of
	/// unlimited length.
	/// <p>
	/// Note that this class NEVER closes the underlying stream, even when close
	/// gets called.  Instead, the stream will be marked as closed and no further
	/// output will be permitted.
	/// </remarks>
	/// <since>4.0</since>
	public class IdentityOutputStream : OutputStream
	{
		/// <summary>Wrapped session output buffer.</summary>
		/// <remarks>Wrapped session output buffer.</remarks>
		private readonly SessionOutputBuffer @out;

		/// <summary>True if the stream is closed.</summary>
		/// <remarks>True if the stream is closed.</remarks>
		private bool closed = false;

		public IdentityOutputStream(SessionOutputBuffer @out) : base()
		{
			this.@out = Args.NotNull(@out, "Session output buffer");
		}

		/// <summary><p>Does not close the underlying socket output.</p></summary>
		/// <exception cref="System.IO.IOException">If an I/O problem occurs.</exception>
		public override void Close()
		{
			if (!this.closed)
			{
				this.closed = true;
				this.@out.Flush();
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Flush()
		{
			this.@out.Flush();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(byte[] b, int off, int len)
		{
			if (this.closed)
			{
				throw new IOException("Attempted write to closed stream.");
			}
			this.@out.Write(b, off, len);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(byte[] b)
		{
			Write(b, 0, b.Length);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Write(int b)
		{
			if (this.closed)
			{
				throw new IOException("Attempted write to closed stream.");
			}
			this.@out.Write(b);
		}
	}
}

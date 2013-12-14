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
	/// <summary>Input stream that reads data without any transformation.</summary>
	/// <remarks>
	/// Input stream that reads data without any transformation. The end of the
	/// content entity is demarcated by closing the underlying connection
	/// (EOF condition). Entities transferred using this input stream can be of
	/// unlimited length.
	/// <p>
	/// Note that this class NEVER closes the underlying stream, even when close
	/// gets called.  Instead, it will read until the end of the stream (until
	/// <code>-1</code> is returned).
	/// </remarks>
	/// <since>4.0</since>
	public class IdentityInputStream : InputStream
	{
		private readonly SessionInputBuffer @in;

		private bool closed = false;

		/// <summary>Wraps session input stream and reads input until the the end of stream.</summary>
		/// <remarks>Wraps session input stream and reads input until the the end of stream.</remarks>
		/// <param name="in">The session input buffer</param>
		public IdentityInputStream(SessionInputBuffer @in) : base()
		{
			this.@in = Args.NotNull(@in, "Session input buffer");
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override int Available()
		{
			if (this.@in is BufferInfo)
			{
				return ((BufferInfo)this.@in).Length();
			}
			else
			{
				return 0;
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Close()
		{
			this.closed = true;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override int Read()
		{
			if (this.closed)
			{
				return -1;
			}
			else
			{
				return this.@in.Read();
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override int Read(byte[] b, int off, int len)
		{
			if (this.closed)
			{
				return -1;
			}
			else
			{
				return this.@in.Read(b, off, len);
			}
		}
	}
}

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
using System.Runtime.Serialization;

namespace Org.Apache.Http.Entity
{
	/// <summary>
	/// A streamed entity that obtains its content from a
	/// <see cref="System.IO.ISerializable">System.IO.ISerializable</see>
	/// .
	/// The content obtained from the
	/// <see cref="System.IO.ISerializable">System.IO.ISerializable</see>
	/// instance can
	/// optionally be buffered in a byte array in order to make the
	/// entity self-contained and repeatable.
	/// </summary>
	/// <since>4.0</since>
	public class SerializableEntity : AbstractHttpEntity
	{
		private byte[] objSer;

        private ISerializable objRef;

		/// <summary>Creates new instance of this class.</summary>
		/// <remarks>Creates new instance of this class.</remarks>
		/// <param name="ser">input</param>
		/// <param name="bufferize">
		/// tells whether the content should be
		/// stored in an internal buffer
		/// </param>
		/// <exception cref="System.IO.IOException">in case of an I/O error</exception>
		public SerializableEntity(ISerializable ser, bool bufferize) : base()
		{
			Args.NotNull(ser, "Source object");
			if (bufferize)
			{
				CreateBytes(ser);
			}
			else
			{
				this.objRef = ser;
			}
		}

		/// <since>4.3</since>
		public SerializableEntity(ISerializable ser) : base()
		{
			Args.NotNull(ser, "Source object");
			this.objRef = ser;
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void CreateBytes(ISerializable ser)
		{
			ByteArrayOutputStream baos = new ByteArrayOutputStream();
			ObjectOutputStream @out = new ObjectOutputStream(baos);
			@out.WriteObject(ser);
			@out.Flush();
			this.objSer = baos.ToByteArray();
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="System.InvalidOperationException"></exception>
		public override InputStream GetContent()
		{
			if (this.objSer == null)
			{
				CreateBytes(this.objRef);
			}
			return new ByteArrayInputStream(this.objSer);
		}

		public override long GetContentLength()
		{
			if (this.objSer == null)
			{
				return -1;
			}
			else
			{
				return this.objSer.Length;
			}
		}

		public override bool IsRepeatable()
		{
			return true;
		}

		public override bool IsStreaming()
		{
			return this.objSer == null;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void WriteTo(OutputStream outstream)
		{
			Args.NotNull(outstream, "Output stream");
			if (this.objSer == null)
			{
				ObjectOutputStream @out = new ObjectOutputStream(outstream);
				@out.WriteObject(this.objRef);
				@out.Flush();
			}
			else
			{
				outstream.Write(this.objSer);
				outstream.Flush();
			}
		}
	}
}

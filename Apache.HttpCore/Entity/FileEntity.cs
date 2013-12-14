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
	/// <summary>A self contained, repeatable entity that obtains its content from a file.
	/// 	</summary>
	/// <remarks>A self contained, repeatable entity that obtains its content from a file.
	/// 	</remarks>
	/// <since>4.0</since>
	public class FileEntity : AbstractHttpEntity, ICloneable
	{
		protected internal readonly FilePath file;

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1.3) FileEntity(Sharpen.FilePath, ContentType)")]
		public FileEntity(FilePath file, string contentType) : base()
		{
			this.file = Args.NotNull(file, "File");
			SetContentType(contentType);
		}

		/// <since>4.2</since>
		public FileEntity(FilePath file, ContentType contentType) : base()
		{
			this.file = Args.NotNull(file, "File");
			if (contentType != null)
			{
				SetContentType(contentType.ToString());
			}
		}

		/// <since>4.2</since>
		public FileEntity(FilePath file) : base()
		{
			this.file = Args.NotNull(file, "File");
		}

		public override bool IsRepeatable()
		{
			return true;
		}

		public override long GetContentLength()
		{
			return this.file.Length();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override InputStream GetContent()
		{
			return new FileInputStream(this.file);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void WriteTo(OutputStream outstream)
		{
			Args.NotNull(outstream, "Output stream");
			InputStream instream = new FileInputStream(this.file);
			try
			{
				byte[] tmp = new byte[OutputBufferSize];
				int l;
				while ((l = instream.Read(tmp)) != -1)
				{
					outstream.Write(tmp, 0, l);
				}
				outstream.Flush();
			}
			finally
			{
				instream.Close();
			}
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
			// File instance is considered immutable
			// No need to make a copy of it
			return base.MemberwiseClone();
		}
	}
}

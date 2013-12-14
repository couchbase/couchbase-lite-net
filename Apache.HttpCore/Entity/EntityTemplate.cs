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
	/// Entity that delegates the process of content generation
	/// to a
	/// <see cref="ContentProducer">ContentProducer</see>
	/// .
	/// </summary>
	/// <since>4.0</since>
	public class EntityTemplate : AbstractHttpEntity
	{
		private readonly ContentProducer contentproducer;

		public EntityTemplate(ContentProducer contentproducer) : base()
		{
			this.contentproducer = Args.NotNull(contentproducer, "Content producer");
		}

		public override long GetContentLength()
		{
			return -1;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override InputStream GetContent()
		{
			ByteArrayOutputStream buf = new ByteArrayOutputStream();
			WriteTo(buf);
			return new ByteArrayInputStream(buf.ToByteArray());
		}

		public override bool IsRepeatable()
		{
			return true;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void WriteTo(OutputStream outstream)
		{
			Args.NotNull(outstream, "Output stream");
			this.contentproducer.WriteTo(outstream);
		}

		public override bool IsStreaming()
		{
			return false;
		}
	}
}

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

using System.Collections.Generic;
using System.IO;
using Sharpen;

namespace Couchbase
{
	public class CBLAttachment
	{
		private InputStream contentStream;

		private string contentType;

		private IDictionary<string, object> metadata;

		private bool gzipped;

		public CBLAttachment()
		{
		}

		public CBLAttachment(InputStream contentStream, string contentType)
		{
			this.contentStream = contentStream;
			this.contentType = contentType;
			metadata = new Dictionary<string, object>();
			metadata.Put("content_type", contentType);
			metadata.Put("follows", true);
			gzipped = false;
		}

		public virtual InputStream GetContentStream()
		{
			return contentStream;
		}

		public virtual void SetContentStream(InputStream contentStream)
		{
			this.contentStream = contentStream;
		}

		public virtual string GetContentType()
		{
			return contentType;
		}

		public virtual void SetContentType(string contentType)
		{
			this.contentType = contentType;
		}

		public virtual bool GetGZipped()
		{
			return gzipped;
		}

		public virtual void SetGZipped(bool gzipped)
		{
			this.gzipped = gzipped;
		}
	}
}

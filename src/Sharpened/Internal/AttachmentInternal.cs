// 
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite.Internal
{
	/// <summary>A simple container for attachment metadata.</summary>
	/// <remarks>A simple container for attachment metadata.</remarks>
	public class AttachmentInternal
	{
		public enum AttachmentEncoding
		{
			AttachmentEncodingNone,
			AttachmentEncodingGZIP
		}

		private string name;

		private string contentType;

		private BlobKey blobKey;

		private long length;

		private long encodedLength;

		private AttachmentInternal.AttachmentEncoding encoding;

		private int revpos;

		public AttachmentInternal(string name, string contentType)
		{
			this.name = name;
			this.contentType = contentType;
		}

		public virtual bool IsValid()
		{
			if (encoding != AttachmentInternal.AttachmentEncoding.AttachmentEncodingNone)
			{
				if (encodedLength == 0 && length > 0)
				{
					return false;
				}
			}
			else
			{
				if (encodedLength > 0)
				{
					return false;
				}
			}
			if (revpos == 0)
			{
				return false;
			}
			return true;
		}

		public virtual string GetName()
		{
			return name;
		}

		public virtual string GetContentType()
		{
			return contentType;
		}

		public virtual BlobKey GetBlobKey()
		{
			return blobKey;
		}

		public virtual void SetBlobKey(BlobKey blobKey)
		{
			this.blobKey = blobKey;
		}

		public virtual long GetLength()
		{
			return length;
		}

		public virtual void SetLength(long length)
		{
			this.length = length;
		}

		public virtual long GetEncodedLength()
		{
			return encodedLength;
		}

		public virtual void SetEncodedLength(long encodedLength)
		{
			this.encodedLength = encodedLength;
		}

		public virtual AttachmentInternal.AttachmentEncoding GetEncoding()
		{
			return encoding;
		}

		public virtual void SetEncoding(AttachmentInternal.AttachmentEncoding encoding)
		{
			this.encoding = encoding;
		}

		public virtual int GetRevpos()
		{
			return revpos;
		}

		public virtual void SetRevpos(int revpos)
		{
			this.revpos = revpos;
		}
	}
}

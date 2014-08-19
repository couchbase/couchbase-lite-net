//
// FormBodyPart.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
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
//

using System;
using System.Text;
using Apache.Http.Entity.Mime;
using Apache.Http.Entity.Mime.Content;
using Sharpen;

namespace Apache.Http.Entity.Mime
{
    /// <summary>
    /// FormBodyPart class represents a content body that can be used as a part of multipart encoded
    /// entities.
    /// </summary>
    /// <remarks>
    /// FormBodyPart class represents a content body that can be used as a part of multipart encoded
    /// entities. This class automatically populates the header with standard fields based on
    /// the content description of the enclosed body.
    /// </remarks>
    /// <since>4.0</since>
    public class FormBodyPart
    {
        private readonly string name;

        private readonly Header header;

        private readonly ContentBody body;

        public FormBodyPart(string name, ContentBody body) : base()
        {
            if (name == null)
            {
                throw new ArgumentException("Name may not be null");
            }
            if (body == null)
            {
                throw new ArgumentException("Body may not be null");
            }
            this.name = name;
            this.body = body;
            this.header = new Header();
            GenerateContentDisp(body);
            GenerateContentType(body);
            GenerateTransferEncoding(body);
        }

        public virtual string GetName()
        {
            return this.name;
        }

        public virtual ContentBody GetBody()
        {
            return this.body;
        }

        public virtual Header GetHeader()
        {
            return this.header;
        }

        public virtual void AddField(string name, string value)
        {
            if (name == null)
            {
                throw new ArgumentException("Field name may not be null");
            }
            this.header.AddField(new MinimalField(name, value));
        }

        protected internal virtual void GenerateContentDisp(ContentBody body)
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("attachment");
            if (body.GetFilename() != null)
            {
                buffer.Append("; filename=\"");
                buffer.Append(body.GetFilename());
                buffer.Append("\"");
            }
            AddField(MIME.ContentDisposition, buffer.ToString());
        }

        protected internal virtual void GenerateContentType(ContentBody body)
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append(body.GetMimeType());
            // MimeType cannot be null
            if (body.GetCharset() != null)
            {
                // charset may legitimately be null
                buffer.Append("; charset=");
                buffer.Append(body.GetCharset());
            }
            AddField(MIME.ContentType, buffer.ToString());
        }

        protected internal virtual void GenerateTransferEncoding(ContentBody body)
        {
            AddField(MIME.ContentTransferEnc, body.GetTransferEncoding());
        }
        // TE cannot be null
    }
}

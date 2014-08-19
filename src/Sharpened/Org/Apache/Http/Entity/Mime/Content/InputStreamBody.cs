//
// InputStreamBody.cs
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
using System.IO;
using Org.Apache.Http.Entity.Mime;
using Org.Apache.Http.Entity.Mime.Content;
using Sharpen;

namespace Org.Apache.Http.Entity.Mime.Content
{
    /// <since>4.0</since>
    public class InputStreamBody : AbstractContentBody
    {
        private readonly InputStream @in;

        private readonly string filename;

        public InputStreamBody(InputStream @in, string mimeType, string filename) : base(
            mimeType)
        {
            if (@in == null)
            {
                throw new ArgumentException("Input stream may not be null");
            }
            this.@in = @in;
            this.filename = filename;
        }

        public InputStreamBody(InputStream @in, string filename) : this(@in, "application/octet-stream"
            , filename)
        {
        }

        public virtual InputStream GetInputStream()
        {
            return this.@in;
        }

        /// <exception cref="System.IO.IOException"></exception>
        [Obsolete]
        [System.ObsoleteAttribute(@"use WriteTo(System.IO.OutputStream)")]
        public virtual void WriteTo(OutputStream @out, int mode)
        {
            WriteTo(@out);
        }

        /// <exception cref="System.IO.IOException"></exception>
        public override void WriteTo(OutputStream @out)
        {
            if (@out == null)
            {
                throw new ArgumentException("Output stream may not be null");
            }
            try
            {
                byte[] tmp = new byte[4096];
                int l;
                while ((l = this.@in.Read(tmp)) != -1)
                {
                    @out.Write(tmp, 0, l);
                }
                @out.Flush();
            }
            finally
            {
                this.@in.Close();
            }
        }

        public override string GetTransferEncoding()
        {
            return MIME.EncBinary;
        }

        public override string GetCharset()
        {
            return null;
        }

        public override long GetContentLength()
        {
            return -1;
        }

        public override string GetFilename()
        {
            return this.filename;
        }
    }
}

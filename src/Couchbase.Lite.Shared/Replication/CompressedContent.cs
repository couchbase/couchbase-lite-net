// CompressedContent.cs
//
// Copyright (c) 2012 Pedro Reys, Chris Missal, Headspring and other contributors
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//     The above copyright notice and this permission notice shall be included in
//     all copies or substantial portions of the Software.
// 
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//     THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    // https://github.com/WebApiContrib/WebAPIContrib/blob/master/src/WebApiContrib/Content/CompressedContent.cs

    /// <summary>
    /// An HttpContent class that works with gzipped or zipped content
    /// </summary>
    public sealed class CompressedContent : HttpContent
    {

        #region Variables

        private readonly HttpContent originalContent;
        private readonly string encodingType;

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        /// <param name="content">The content to compress.</param>
        /// <param name="encodingType">The compression type to use (gzip or deflate).</param>
        public CompressedContent(HttpContent content, string encodingType)
        {
            if (content == null) {
                throw new ArgumentNullException("content");
            }

            if (encodingType == null) {
                throw new ArgumentNullException("encodingType");
            }

            originalContent = content;
            this.encodingType = encodingType.ToLowerInvariant();

            if (this.encodingType != "gzip" && this.encodingType != "deflate") {
                throw new InvalidOperationException(string.Format("Encoding '{0}' is not supported. Only supports gzip or deflate encoding.", this.encodingType));
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in originalContent.Headers) {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Headers.ContentEncoding.Add(encodingType);
        }

        #endregion

        #region Overrides
        #pragma warning disable 1591

        protected override bool TryComputeLength(out long length)
        {
            length = -1;

            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return originalContent.ReadAsStreamAsync();
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            Stream compressedStream = null;

            if (encodingType == "gzip")
            {
                compressedStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
            }
            else if (encodingType == "deflate")
            {
                compressedStream = new DeflateStream(stream, CompressionMode.Compress, leaveOpen: true);
            }

            var retVal = originalContent.CopyToAsync(compressedStream);
            retVal.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
            {
                if (compressedStream != null)
                {
                    compressedStream.Dispose();
                }
            });

            return retVal;
        }

        #pragma warning restore 1591
        #endregion
    }
}


//
// AttachmentInternal.cs
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

using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;
using System.Collections.Generic;
using System.IO;
using System;
using System.Diagnostics;
using System.Linq;
using System.IO.Compression;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal
{
    internal enum AttachmentEncoding
    {
        None,
        GZIP
    }
        
    internal sealed class AttachmentInternal
    {
        private IEnumerable<byte> _data;
        private const string TAG = "AttachmentInternal";

        public long Length { get; set; }

        public long EncodedLength { get; set; }

        public AttachmentEncoding Encoding { get; set; }

        public int RevPos { get; set; }

        public Database Database { get; set; }

        public string Name { get; private set; }

        public string ContentType { get; private set; }

        public BlobKey BlobKey { 
            get { return _blobKey; }
            set { 
                _digest = null;
                _blobKey = value;
            }
        }
        private BlobKey _blobKey;

        public string Digest { 
            get { 
                if (_digest != null) {
                    return _digest;
                }

                if (_blobKey != null) {
                    return _blobKey.Base64Digest();
                }

                return null;
            }
        }
        private string _digest;

        // only if inline or stored in db blob-store 
        public IEnumerable<byte> EncodedContent { 
            get {
                if (_data != null) {
                    return _data;
                }

                return Database.Attachments.BlobForKey(_blobKey);
            }
        }

        public IEnumerable<byte> Content { 
            get {
                var data = EncodedContent;
                switch (Encoding) {
                    case AttachmentEncoding.None:
                        break;
                    case AttachmentEncoding.GZIP:
                        data = data.Decompress();
                        break;
                }

                if (data == null) {
                    Log.W(TAG, "Unable to decode attachment!");
                }

                return data;
            }
        }

        public Stream ContentStream { 
            get {
                if (Encoding == AttachmentEncoding.None) {
                    return Database.Attachments.BlobStreamForKey(_blobKey);
                }

                var ms = new MemoryStream(_data.ToArray());
                return new GZipStream(ms, CompressionMode.Decompress, true);
            }
        }

        // only if already stored in db blob-store
        public Uri ContentUrl { 
            get {
                string path = Database.Attachments.PathForKey(_blobKey);
                return path != null ? new Uri(path) : null;
            }
        }

        public bool IsValid { 
            get {
                if (Encoding != AttachmentEncoding.None) {
                    if (EncodedLength == 0 && Length > 0) {
                        return false;
                    }
                } else if (EncodedLength > 0) {
                    return false;
                }

                if (RevPos == 0) {
                    return false;
                }

                #if DEBUG
                if(_blobKey == null) {
                    return false;
                }
                #endif

                return true;
            }
        }

        public AttachmentInternal(string name, string contentType)
        {
            Debug.Assert(name != null);
            Name = name;
            ContentType = contentType;
        }

        public AttachmentInternal(string name, IDictionary<string, object> info) 
            : this(name, info.GetCast<string>("content_type"))
        {
            Length = info.GetCast<long>("length");
            EncodedLength = info.GetCast<long>("encoded_length");
            _digest = info.GetCast<string>("digest");
            if (_digest != null) {
                BlobKey = new BlobKey(Digest);
            }

            string encodingString = info.GetCast<string>("encoding");
            if (encodingString != null) {
                if (encodingString.Equals("gzip")) {
                    Encoding = AttachmentEncoding.GZIP;
                } else {
                    throw new CouchbaseLiteException(StatusCode.BadEncoding);
                }
            }

            var data = info.Get("Data");
            if (data != null) {
                // If there's inline attachment data, decode and store it:
                if (data is string) {
                    _data = Convert.FromBase64String((string)data);
                } else {
                    _data = data as IEnumerable<byte>;
                }

                if (_data == null) {
                    throw new CouchbaseLiteException(StatusCode.BadEncoding);
                }

                SetPossiblyEncodedLength(((IEnumerable<byte>)data).LongCount());
            } else if (info.GetCast<bool>("stub", false)) {
                // This item is just a stub; validate and skip it
                int revPos = (int)info.GetCast<long>("revpos");
                if (revPos <= 0) {
                    throw new CouchbaseLiteException(StatusCode.BadAttachment);
                }

                RevPos = revPos;
            } else if (info.GetCast<bool>("follows", false)) {
                // I can't handle this myself; my caller will look it up from the digest
                if (_digest == null) {
                    throw new CouchbaseLiteException(StatusCode.BadAttachment);
                }
            } else {
                throw new CouchbaseLiteException(StatusCode.BadAttachment);
            }
        }
            
        public IDictionary<string, object> AsStubDictionary()
        {
            var retVal = new Dictionary<string, object> {
                { "stub", true },
                { "digest", _blobKey.Base64Digest() },
                { "content_type", ContentType },
                { "revpos", RevPos },
                { "length", Length }
            };

            if (EncodedLength > 0) {
                retVal["encoded_length"] = EncodedLength;
            }

            switch (Encoding) {
                case AttachmentEncoding.GZIP:
                    retVal["encoding"] = "gzip";
                    break;
                case AttachmentEncoding.None:
                    break;
            }

            return retVal;
        }

        public void SetPossiblyEncodedLength(long length)
        {
            if (Encoding != AttachmentEncoding.None) {
                EncodedLength = length;
            } else {
                Length = length;
            }
        }
    }
}

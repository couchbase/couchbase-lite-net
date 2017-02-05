//
//  Blob.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Serialization;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    public interface IBlob
    {
        byte[] Content { get; }

        Stream ContentStream { get; }

        string ContentType { get; }

        ulong Length { get; }

        string Digest { get; }

        IReadOnlyDictionary<string, object> Properties { get; }
    }

    public static class BlobFactory
    {
        public static IBlob Create(string contentType, byte[] content)
        {
            return new Blob(contentType, content);
        }

        public static IBlob Create(string contentType, Stream stream)
        {
            return new Blob(contentType, stream);
        }

        public static IBlob Create(string contentType, Uri fileUrl)
        {
            return new Blob(contentType, fileUrl);
        }
    }

    internal sealed unsafe class Blob : IBlob
    {
        private const string TypeMetaProperty = "_cbltype";
        private const string BlobType = "blob";
        private const uint MaxCachedContentLength = 8 * 1024;
        private const int ReadBufferSize = 8 * 1024;
        private const string Tag = nameof(Blob);

        private Stream _initialContentStream;
        private byte[] _content;
        private Database _db;
        private Dictionary<string, object> _properties;

        public byte[] Content
        {
            get {
                if(_content != null) {
                    return _content;
                }

                if(_db != null) {
                    C4BlobStore* blobStore;
                    C4BlobKey key;
                    if(!GetBlobStore(&blobStore, &key)) {
                        return null;
                    }

                    //TODO: If data is large, can get the file path & memory-map it
                    var content = Native.c4blob_getContents(blobStore, key, null);
                    if(content?.Length <= MaxCachedContentLength) {
                        _content = content;
                    }

                    return content;
                } else {
                    if(_initialContentStream == null) {
                        throw new InvalidOperationException("Blob has no data available");
                    }

                    var result = new List<byte>();
                    var buffer = default(byte[]);
                    using(var reader = new BinaryReader(_initialContentStream)) {
                        do {
                            buffer = reader.ReadBytes(ReadBufferSize);
                            result.AddRange(buffer);
                        } while(buffer.Length == ReadBufferSize);
                    }

                    _initialContentStream = null;
                    _content = result.ToArray();
                    Length = (ulong)_content.Length;
                    return _content;
                }
            }
        }

        public Stream ContentStream
        {
            get {
                if(_db != null) {
                    C4BlobStore* blobStore;
                    C4BlobKey key;
                    if(!GetBlobStore(&blobStore, &key)) {
                        return null;
                    }

                    return new BlobReadStream(blobStore, key);
                } else {
                    return _content != null ? new MemoryStream(_content) : null;
                }
            }
        }

        public string ContentType { get; }

        public ulong Length { get; private set; }

        public string Digest { get; }

        public IReadOnlyDictionary<string, object> Properties
        {
            get {
                if(_properties != null) {
                    return new ReadOnlyDictionary<string, object>(_properties);
                }

                return new NonNullDictionary<string, object> {
                    ["digest"] = Digest,
                    ["length"] = Length > 0 ? (object)Length : null,
                    ["content-type"] = ContentType
                };
            }
        }

        public IReadOnlyDictionary<string, object> JsonRepresentation
        {
            get {
                if(_db == null) {
                    throw new InvalidOperationException("Blob hasn't been saved in the database yet");
                }

                var json = new Dictionary<string, object>(_properties);
                json[TypeMetaProperty] = BlobType;
                return json;
            }
        }

        public Blob(string contentType, byte[] content)
        {
            if(content == null) {
                throw new ArgumentNullException(nameof(content));
            }

            ContentType = contentType;
            _content = content;
            Length = (ulong)content.Length;
        }

        public Blob(string contentType, Stream stream)
        {
            if(stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            ContentType = contentType;
            _initialContentStream = stream;
        }

        public Blob(string contentType, Uri fileURL)
        {
            if(fileURL == null) {
                throw new ArgumentNullException(nameof(fileURL));
            }

            if(!fileURL.IsFile) {
                throw new ArgumentException($"{fileURL} must be a file-based URL", nameof(fileURL));
            }

            ContentType = contentType;
            _initialContentStream = File.OpenRead(fileURL.AbsolutePath);
        }

        internal Blob(Database db, IDictionary<string, object> properties)
        {
            if(db == null) {
                throw new ArgumentNullException(nameof(db));
            }

            if(properties == null) {
                throw new ArgumentNullException(nameof(properties));
            }

            _db = db;
            _properties = new Dictionary<string, object>(properties);
            _properties[TypeMetaProperty] = null;
            Length = properties.GetCast<ulong>("length");
            Digest = properties.GetCast<string>("digest");
            if(Digest == null) {
                Log.To.Database.W(Tag, "Blob read from database has missing digest");
            }
        }

        internal void Install(Database db)
        {
            if(db == null) {
                throw new ArgumentNullException(nameof(db));
            }

            if(_db != null) {
                if(db != _db) {
                    throw new InvalidOperationException("Blob belongs to a different database");
                }

                return;
            }

            var store = db.BlobStore;
            var key = default(C4BlobKey);
            if(_content != null) {
                LiteCoreBridge.Check(err =>
                {
                    var tmpKey = default(C4BlobKey);
                    var s = Native.c4blob_create(store, _content, &tmpKey, err);
                    key = tmpKey;
                    return s;
                });
            } else {
                if(_initialContentStream != null) {
                    throw new InvalidOperationException("No data available to write for install");
                }

                Length = 0;
                var contentStream = ContentStream;
                using(var blobOut = new BlobWriteStream(store)) {
                    contentStream.CopyTo(blobOut, ReadBufferSize);
                    blobOut.Flush();
                    key = blobOut.Key;
                }
            }
        }

        private bool GetBlobStore(C4BlobStore** outBlobStore, C4BlobKey* outKey)
        {
            try {
                *outBlobStore = _db.BlobStore;
                return Digest != null && Native.c4blob_keyFromString(Digest, outKey);
            } catch(InvalidOperationException) {
                return false;
            }
        }

        public override string ToString()
        {
            return $"Blob[{ContentType}; {(Length + 512) / 1024} KB]";
        }
    }
}

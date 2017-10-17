// 
// Blob.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite
{

    /// <summary>
    /// A class representing an arbitrary piece of binary data
    /// </summary>
    public sealed unsafe class Blob
    {
        #region Constants
        
        private const uint MaxCachedContentLength = 8 * 1024;
        private const int ReadBufferSize = 8 * 1024;
        private const string Tag = nameof(Blob);

        #endregion

        #region Variables

        private readonly Dictionary<string, object> _properties;
        private byte[] _content;
        private Database _db;
        private Stream _initialContentStream;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the contents of the blob as an in-memory array
        /// </summary>
        public byte[] Content
        {
            get {
                if(_content != null) {
                    return _content;
                }

                if (_db != null) {
                    C4BlobStore* blobStore;
                    C4BlobKey key;
                    if (!GetBlobStore(&blobStore, &key)) {
                        return null;
                    }

                    //TODO: If data is large, can get the file path & memory-map it
                    var content = Native.c4blob_getContents(blobStore, key, null);
                    if (content?.Length <= MaxCachedContentLength) {
                        _content = content;
                    }

                    return content;
                }

                if(_initialContentStream == null) {
                    throw new InvalidOperationException("Blob has no data available");
                }

                var result = new List<byte>();
                using(var reader = new BinaryReader(_initialContentStream)) {
                    byte[] buffer;
                    do {
                        buffer = reader.ReadBytes(ReadBufferSize);
                        result.AddRange(buffer);
                    } while(buffer.Length == ReadBufferSize);
                }

                _initialContentStream.Dispose();
                _initialContentStream = null;
                _content = result.ToArray();
                Length = (ulong)_content.Length;
                return _content;
            }
        }

        /// <summary>
        /// Gets the contents of the blob as a <see cref="Stream"/>
        /// </summary>
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

        /// <summary>
        /// Gets the content type of the blob
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Gets the digest of the blob, once it is saved
        /// </summary>
        public string Digest { get; private set; }

        /// <summary>
        /// Gets the length of the data that the blob contains
        /// </summary>
        public ulong Length { get; private set; }

        /// <summary>
        /// Gets the metadata of the blob instance
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties => new ReadOnlyDictionary<string, object>(MutableProperties);

        internal IReadOnlyDictionary<string, object> JsonRepresentation
        {
            get {
                if (_db == null) {
                    throw new InvalidOperationException("Blob hasn't been saved in the database yet");
                }

                var json = new Dictionary<string, object>(MutableProperties) {
                    [Constants.ObjectTypeProperty] = Constants.ObjectTypeBlob
                };

                return json;
            }
        }

        private IDictionary<string, object> MutableProperties
        {
            get {
                if(_properties != null) {
                    return _properties;
                }

                return new NonNullDictionary<string, object> {
                    ["digest"] = Digest,
                    ["length"] = Length > 0 ? (object)Length : null,
                    ["content-type"] = ContentType
                };
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a blob given a type and in memory content
        /// </summary>
        /// <param name="contentType">The binary type of the blob</param>
        /// <param name="content">The content of the blob</param>
        /// <returns>An instantiated <see cref="Blob" /> object</returns>
        /// <exception cref="ArgumentNullException">Thrown if <c>content</c> is <c>null</c></exception>
        public Blob(string contentType, byte[] content)
        {
            ContentType = contentType;
            _content = content ?? throw new ArgumentNullException(nameof(content));
            Length = (ulong)content.Length;
        }

        /// <summary>
        /// Creates a blob given a type and streaming content
        /// </summary>
        /// <param name="contentType">The binary type of the blob</param>
        /// <param name="stream">The stream containing the blob content</param>
        /// <returns>An instantiated <see cref="Blob" /> object</returns>
        /// <exception cref="ArgumentNullException">Thrown if <c>stream</c> is <c>null</c></exception>
        public Blob(string contentType, Stream stream)
        {
            ContentType = contentType;
            _initialContentStream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        /// <summary>
        /// Creates an blob given a type and a URL to a file
        /// </summary>
        /// <param name="contentType">The binary type of the blob</param>
        /// <param name="fileUrl">The url to the file to read</param>
        /// <returns>An instantiated <see cref="Blob" /> object</returns>
        /// <exception cref="ArgumentNullException">Thrown if <c>fileUrl</c> is <c>null</c></exception>
        /// <exception cref="ArgumentException">Thrown if fileUrl is not a file based URL</exception>
        /// <exception cref="DirectoryNotFoundException">The specified fileUrl is invalid, 
        /// (for example, it is on an unmapped drive).</exception>
        /// <exception cref="UnauthorizedAccessException">fileUrl specified a directory -or- The caller 
        /// does not have the required permission.</exception>
        /// <exception cref="FileNotFoundException">The file specified in fileUrl was not found.</exception>
        /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
        public Blob(string contentType, Uri fileUrl)
        {
            if(fileUrl == null) {
                throw new ArgumentNullException(nameof(fileUrl));
            }

            if(!fileUrl.IsFile) {
                throw new ArgumentException($"{fileUrl} must be a file-based URL", nameof(fileUrl));
            }

            ContentType = contentType;
            _initialContentStream = File.OpenRead(fileUrl.AbsolutePath);
        }

        internal Blob(Database db, IDictionary<string, object> properties)
        {
            if(properties == null) {
                throw new ArgumentNullException(nameof(properties));
            }

            _db = db ?? throw new ArgumentNullException(nameof(db));
            _properties = new Dictionary<string, object>(properties);
            _properties.Remove(Constants.ObjectTypeProperty);

            Length = properties.GetCast<ulong>("length");
            Digest = properties.GetCast<string>("digest");
            if(Digest == null) {
                Log.To.Database.W(Tag, "Blob read from database has missing digest");
            }
        }

        #endregion

        #region Internal Methods

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
                LiteCoreBridge.Check(err => {
                    var tmpKey = default(C4BlobKey);
                    var s = Native.c4blob_create(store, _content, null, &tmpKey, err);
                    key = tmpKey;
                    return s;
                });
            } else {
                if(_initialContentStream == null) {
                    throw new InvalidOperationException("No data available to write for install");
                }

                Length = 0;
                var contentStream = _initialContentStream;
                using(var blobOut = new BlobWriteStream(store)) {
                    contentStream.CopyTo(blobOut, ReadBufferSize);
                    blobOut.Flush();
                    key = blobOut.Key;
                }

                _initialContentStream.Dispose();
                _initialContentStream = null;
            }

            Digest = Native.c4blob_keyToString(key);
            _db = db;
        }

        #endregion

        #region Private Methods

        private bool GetBlobStore(C4BlobStore** outBlobStore, C4BlobKey* outKey)
        {
            try {
                *outBlobStore = _db.BlobStore;
                return Digest != null && Native.c4blob_keyFromString(Digest, outKey);
            } catch(InvalidOperationException) {
                return false;
            }
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"Blob[{ContentType}; {(Length + 512) / 1024} KB]";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <returns><c>true</c> if the specified object is equal to the current object; otherwise, <c>false</c>.</returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            if (obj is Blob other) {
                if (Digest != null && other.Digest != null) {
                    return Digest.Equals(other.Digest);
                }

                if (Length != other.Length) {
                    return false;
                }
                
                using (var stream1 = ContentStream)
                using (var stream2 = other.ContentStream) {
                    if (stream1 == null) {
                        return stream2 == null;
                    }

                    if (stream2 == null) {
                        return false;
                    }

                    int next1;
                    while((next1 = stream1.ReadByte()) != -1) {
                        var next2 = stream2.ReadByte();
                        if (next1 != next2) {
                            return false;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}

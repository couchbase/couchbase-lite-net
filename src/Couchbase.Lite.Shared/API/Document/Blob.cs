// 
// Blob.cs
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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

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

        private const string ContentTypeKey = "content_type";
        private const string DigestKey = "digest";
        private const string LengthKey = "length";
        private const string DataKey = "data";

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
        /// <exception cref="InvalidOperationException">Thrown if this blob has no associated data (unusual)</exception>
        [CanBeNull]
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
                    C4Error err;
                    var content = Native.c4blob_getContents(blobStore, key, &err);
                    if (err.domain == C4ErrorDomain.LiteCoreDomain && err.code == (int)C4ErrorCode.NotFound) {
                        WriteLog.To.Database.W(Tag,
                            "Blob in database has no data (are you calling Blob.Content from a pull filter function?), returning null...");
                        return null;
                    }

                    if (err.code > 0) {
                        throw CouchbaseException.Create(err);
                    }

                    if (content?.Length <= MaxCachedContentLength) {
                        _content = content;
                    }

                    return content;
                }

                if(_initialContentStream == null) {
                    throw new InvalidOperationException(CouchbaseLiteErrorMessage.BlobContainsNoData);
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
                Length = _content.Length;
                return _content;
            }
        }

        /// <summary>
        /// Gets the contents of the blob as a <see cref="Stream"/>
        /// </summary>
        /// <remarks>
        /// The caller is responsible for disposing the Stream when finished with it.
        /// </remarks>
        [CanBeNull]
        public Stream ContentStream
        {
            get {
                if(_db != null) {
                    C4BlobStore* blobStore;
                    C4BlobKey key;
                    if(GetBlobStore(&blobStore, &key)) {
                        return new BlobReadStream(blobStore, key);
                    }
                }
                return _content != null ? new MemoryStream(_content) : null;
            }
        }

        /// <summary>
        /// Gets the content type of the blob
        /// </summary>
        [CanBeNull]
        public string ContentType { get; }

        /// <summary>
        /// Gets the digest of the blob, once it is saved
        /// </summary>
        [CanBeNull]
        public string Digest { get; private set; }

        /// <summary>
        /// Gets the length of the data that the blob contains
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// Gets the metadata of the blob instance
        /// </summary>
        [NotNull]
        public IReadOnlyDictionary<string, object> Properties => new ReadOnlyDictionary<string, object>(MutableProperties);

        [NotNull]
        internal IReadOnlyDictionary<string, object> JsonRepresentation
        {
            get {
                var json = new Dictionary<string, object>(MutableProperties) {
                    [Constants.ObjectTypeProperty] = Constants.ObjectTypeBlob,
                    [LengthKey] = Length > 0 ? (object)Length : null
                };
                if (Digest != null) {
                    json[DigestKey] = Digest;
                } else {
                    json[DataKey] = Content;
                }

                return json;
            }
        }

        [NotNull]
        private IDictionary<string, object> MutableProperties
        {
            get {
                if(_properties != null) {
                    return _properties;
                }

                return new NonNullDictionary<string, object> {
                    [DigestKey] = Digest,
                    [LengthKey] = Length > 0 ? (object)Length : null,
                    [ContentTypeKey] = ContentType
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
        public Blob(string contentType, [NotNull]byte[] content)
        {
            ContentType = contentType;
            _content = CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(content), content);
            Length = content.Length;
        }

        /// <summary>
        /// Creates a blob given a type and streaming content
        /// </summary>
        /// <param name="contentType">The binary type of the blob</param>
        /// <param name="stream">The stream containing the blob content</param>
        /// <returns>An instantiated <see cref="Blob" /> object</returns>
        /// <exception cref="ArgumentNullException">Thrown if <c>stream</c> is <c>null</c></exception>
        public Blob(string contentType, [NotNull]Stream stream)
        {
            ContentType = contentType;
            _initialContentStream = CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(stream), stream);
        }

        /// <summary>
        /// Creates an blob given a type and a URL to a file
        /// </summary>
        /// <param name="contentType">The binary type of the blob</param>
        /// <param name="fileUrl">The url to the file to read</param>
        /// <returns>An instantiated <see cref="Blob" /> object</returns>
        /// <exception cref="ArgumentNullException">Thrown if <c>fileUrl</c> is <c>null</c></exception>
        /// <exception cref="ArgumentException">Thrown if fileUrl is not a file based URL</exception>
        public Blob(string contentType, [NotNull]Uri fileUrl)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(fileUrl), fileUrl);

            if(!fileUrl.IsFile) {
                throw new ArgumentException(String.Format(CouchbaseLiteErrorMessage.NotFileBasedURL, fileUrl), nameof(fileUrl));
            }

            ContentType = contentType;
            _initialContentStream = File.OpenRead(fileUrl.AbsolutePath);
        }

        internal Blob([NotNull]Database db, [NotNull]IDictionary<string, object> properties)
        {
            SetupProperties(properties);
            _db = CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(db), db);
            _properties = new Dictionary<string, object>(CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, 
                nameof(properties), properties));
            _content = properties.GetCast<byte[]>(DataKey);
            ContentType = properties.GetCast<string>(ContentTypeKey);
            if(Digest == null && _content == null) {
                WriteLog.To.Database.W(Tag, "Blob read from database has neither digest nor data.");
            }
        }

        #endregion

        #region Internal Methods

        internal void FLEncode(FLEncoder* enc)
        {
            var extra = Native.FLEncoder_GetExtraInfo(enc);
            if (extra != null) {
                // This blob is attached to a document, so save the full metadata
                var document = GCHandle.FromIntPtr((IntPtr) extra).Target as MutableDocument;
                var database = document.Database;
                try {
                    Install(database);
                } catch (Exception) {
                    WriteLog.To.Database.W(Tag, "Error installing blob to database, throwing...");
                    throw;
                }
            }
            JsonRepresentation.FLEncode(enc);
        }

        internal void FLSlotSet(FLSlot* slot)
        {
            JsonRepresentation.FLSlotSet(slot);
        }

        #endregion

        #region Private Methods

        private void SetupProperties([NotNull] IDictionary<string, object> properties)
        {
            properties.Remove(Constants.ObjectTypeProperty);

            Length = properties.GetCast<int>(LengthKey);
            Digest = properties.GetCast<string>(DigestKey);
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

        private void Install([NotNull]Database db)
        {
            Debug.Assert(db != null);

            if(_db != null) {
                if(db != _db) {
                    throw new InvalidOperationException(CouchbaseLiteErrorMessage.BlobDifferentDatabase);
                }

                return;
            }

            var store = db.BlobStore;
            C4BlobKey key;
            if(_content != null) {
                LiteCoreBridge.Check(err =>
                {
                    C4BlobKey tmpKey;
                    var s = Native.c4blob_create(store, _content, null, &tmpKey, err);
                    key = tmpKey;
                    return s;
                });
            } else {
                if(_initialContentStream == null) {
                    throw new InvalidOperationException(CouchbaseLiteErrorMessage.BlobContentNull);
                }

                Length = 0;
                var contentStream = _initialContentStream;
                using(var blobOut = new BlobWriteStream(store)) {
                    contentStream.CopyTo(blobOut, ReadBufferSize);
                    blobOut.Flush();
                    Length = blobOut.Length > Int32.MaxValue ? 0 : (int) blobOut.Length;
                    key = blobOut.Key;
                }

                _initialContentStream.Dispose();
                _initialContentStream = null;
            }

            Digest = Native.c4blob_keyToString(key);
            _db = db;
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <returns><c>true</c> if the specified object is equal to the current object; otherwise, <c>false</c>.</returns>
        /// <param name="obj">The object to compare with the current object. </param>
#pragma warning disable 659
        public override bool Equals(object obj)
#pragma warning restore 659
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

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"Blob[{ContentType}; {(Length + 512) / 1024} KB]";
        }

        #endregion
    }
}

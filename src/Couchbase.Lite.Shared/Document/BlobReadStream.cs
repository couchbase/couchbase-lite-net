//
//  BlobReadStream.cs
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
using System.Diagnostics;
using System.IO;

using Couchbase.Lite.Interop;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class BlobReadStream : Stream
    {
        #region Variables

        private long _length = -1;
        private long _position;
        private C4ReadStream* _readStream;

        #endregion

        #region Properties

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length
        {
            get {
                if(_length > -1) {
                    return _length;
                }

                C4Error err;
                var retVal = Native.c4stream_getLength(_readStream, &err);
                if(err.code != 0) {
                    throw CouchbaseException.Create(err);
                }

                _length = retVal;
                return retVal;
            }
        }

        public override long Position
        {
            get => _position;
            set {
                _position = value;
                Seek(Position, SeekOrigin.Begin);
            }
        }

        #endregion

        #region Constructors

        public BlobReadStream([NotNull]C4BlobStore *store, C4BlobKey key)
        {
            Debug.Assert(store != null);

            _readStream = (C4ReadStream*)LiteCoreBridge.Check(err => Native.c4blob_openReadStream(store, key, err));
        }

        #endregion

        #region Overrides

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Native.c4stream_close(_readStream);
            _readStream = null;
        }

        public override void Flush()
        {
            // No-op
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if(_readStream == null) {
                throw new InvalidOperationException("Stream is not open");
            }

            int retVal = 0;
            LiteCoreBridge.Check(err => retVal = (int)Native.c4stream_read(_readStream, buffer, err));
            _position += retVal;
            return retVal;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if(origin == SeekOrigin.Begin) {
                if(offset < 0 || offset > Length) {
                    throw new ArgumentOutOfRangeException(nameof(offset), $"The offset must be between 0 and {Length} for SeekOrigin.Begin");
                }

                LiteCoreBridge.Check(err => Native.c4stream_seek(_readStream, (ulong)offset, err));
                _position = offset;
            } else if(origin == SeekOrigin.Current) {
                var newPos = _position + offset;
                if(newPos < 0 || newPos > Length) {
                    throw new ArgumentOutOfRangeException(nameof(offset), 
                        $"The offset {offset} would result in an invalid position {newPos} (needs to be between 0 and {Length})");
                }

                LiteCoreBridge.Check(err => Native.c4stream_seek(_readStream, (ulong)newPos, err));
                _position = newPos;
            } else {
                var newPos = Length + offset;
                if(newPos < 0 || newPos > Length) {
                    throw new ArgumentOutOfRangeException(nameof(offset),
                        $"The offset {offset} would result in an invalid position {newPos} (needs to be between 0 and {Length})");
                }

                LiteCoreBridge.Check(err => Native.c4stream_seek(_readStream, (ulong)newPos, err));
                _position = newPos;
            }

            return _position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}

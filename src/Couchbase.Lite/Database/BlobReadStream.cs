//
//  BlobStream.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite
{
    internal unsafe sealed class BlobReadStream : Stream
    {
        private C4ReadStream* _readStream;
        private long _position;

        public override bool CanRead
        {
            get {
                return true;
            }
        }

        public override bool CanSeek
        {
            get {
                return true;
            }
        }

        public override bool CanWrite
        {
            get {
                return false;
            }
        }

        private long _length = -1;
        public override long Length
        {
            get {
                if(_length > -1) {
                    return _length;
                }

                C4Error err;
                var retVal = Native.c4stream_getLength(_readStream, &err);
                if(err.code != 0) {
                    throw new LiteCoreException(err);
                }

                _length = retVal;
                return retVal;
            }
        }

        public override long Position
        {
            get {
                return _position;
            }

            set {
                Seek(Position, SeekOrigin.Begin);
                _position = value;
            }
        }

        public BlobReadStream(C4BlobStore *store, C4BlobKey key)
        {
            _readStream = (C4ReadStream*)LiteCoreBridge.Check(err => Native.c4blob_openReadStream(store, key, err));
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

        protected override void Dispose(bool disposing)
        {
            Native.c4stream_close(_readStream);
            _readStream = null;
        }
    }
}

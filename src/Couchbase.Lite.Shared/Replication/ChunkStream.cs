//
// ChunkStream.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Couchbase.Lite.Internal
{
    internal sealed class ChunkStream : Stream
    {
        private BlockingCollection<byte> _chunkQueue = new BlockingCollection<byte>();

        #region Overrides

        public override void Flush()
        {
            _chunkQueue.CompleteAdding();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var readCount = 0;
            for (int i = 0; i < count; i++) {
                byte next;
                if(!_chunkQueue.TryTake(out next, -1)) {
                    break;
                }

                buffer[offset + i] = next;
                readCount++;
            }

            return readCount;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_chunkQueue.IsAddingCompleted) {
                throw new ObjectDisposedException("ChunkStream");
            }

            for (int i = offset; i < offset + count; i++) {
                _chunkQueue.Add(buffer[i + offset]);
            }
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }
        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }
        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }
        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }
        #endregion
    }
}


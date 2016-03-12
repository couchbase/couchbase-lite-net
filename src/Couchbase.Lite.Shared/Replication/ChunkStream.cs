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
using System.Linq;

namespace Couchbase.Lite.Internal
{

    // This is a utility class that facilitates the parsing of continuous 
    // changes received over a web socket.  The change is broken up into
    // pieces but is actually one long message, so this stream will act as
    // one stream while accepting message inputs
    internal sealed class ChunkStream : Stream
    {

        #region Variables

        private BlockingCollection<Queue<byte>> _chunkQueue = new BlockingCollection<Queue<byte>>();
        private Queue<byte> _current;

        #endregion

        #region Properties

        public override bool CanRead
        {
            get {
                return true;
            }
        }

        public override bool CanSeek
        {
            get {
                return false;
            }
        }

        public override bool CanWrite
        {
            get {
                return true;
            }
        }

        public override long Length
        {
            get {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get {
                throw new NotSupportedException();
            }
            set {
                throw new NotSupportedException();
            }
        }

        #endregion

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
                if (_current == null || _current.Count == 0) {
                    // We only want to block if we have no data available, otherwise we might wait
                    // too long before allowing another change to be processed
                    var success = i == 0 ? _chunkQueue.TryTake(out _current, -1) : _chunkQueue.TryTake(out _current);
                    if (!success) {
                        break;
                    }
                }

                // We have a new batch of data, so continue to copy it into the buffer
                buffer[offset + i] = _current.Dequeue();
                readCount++;
            }

            return readCount;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_chunkQueue.IsAddingCompleted) {
                throw new ObjectDisposedException("ChunkStream");
            }

            _chunkQueue.Add(new Queue<byte>(buffer.Skip(offset).Take(count)));
        }

        #endregion
    }
}


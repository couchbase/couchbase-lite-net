//
// ChunkedGZipStream.cs
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
using ICSharpCode.SharpZipLib.GZip;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal
{
    internal enum ChunkStyle
    {
        ByArray,
        ByObject
    }

    internal sealed class ChunkedGZipChanges : IDisposable
    {
        private static readonly string Tag = typeof(ChunkedGZipChanges).Name;

        private readonly ChunkStream _innerStream;
        private readonly GZipInputStream _readStream;
        private readonly ChunkStyle _style;

        public event TypedEventHandler<ChunkedGZipChanges, IDictionary<string, object>> ChunkFound;

        public event TypedEventHandler<ChunkedGZipChanges, Exception> Finished;

        public ChunkedGZipChanges(ChunkStyle style)
        {
            _innerStream = new ChunkStream();
            _readStream = new GZipInputStream(_innerStream) { IsStreamOwner = true };
            _style = style;
            Task.Factory.StartNew(Process);
        }

        public void Complete()
        {
            _innerStream.Flush();
        }

        public void AddData(IEnumerable<byte> data)
        {
            var realized = data.ToArray();
            _innerStream.Write(realized, 0, realized.Length);
        }

        private void Process()
        {
            List<byte> parseBuffer = new List<byte>();
            var nextChar = _readStream.ReadByte();
            var nestedCount = 0;
            var exception = default(Exception);
            while (nextChar != -1) {
                parseBuffer.Add((byte)nextChar);
                if(IsEnd((byte)nextChar)) {
                    if(--nestedCount == 0 && parseBuffer.Count > 0) {
                        var changes = Manager.GetObjectMapper().ReadValue<IList<object>>(parseBuffer);
                        foreach(var change in changes) {
                            if (ChunkFound != null) {
                                ChunkFound(this, change.AsDictionary<string, object>());
                            }
                        }

                        parseBuffer.Clear();
                    }
                } else if(IsStart((byte)nextChar)) {
                    nestedCount++;
                }

                try {
                    nextChar = _readStream.ReadByte();
                } catch(Exception e) {
                    exception = e;
                    Log.To.ChangeTracker.E(Tag, "Failed to read from changes feed, sending to callback...", e);
                    nextChar = -1;
                }
            }

            if (Finished != null) {
                Finished(this, exception);
            }
        }

        private bool IsEnd(byte nextChar) {
            return (_style == ChunkStyle.ByArray && nextChar == ']') ||
            (_style == ChunkStyle.ByObject && nextChar == '}');
        }

        private bool IsStart(byte nextChar) {
            return (_style == ChunkStyle.ByArray && nextChar == '[') ||
                (_style == ChunkStyle.ByObject && nextChar == '{');
        }

        public void Dispose()
        {
            _readStream.Dispose();
        }
    }
}


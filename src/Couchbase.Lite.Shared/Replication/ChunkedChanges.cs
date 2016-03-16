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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Couchbase.Lite.Util;
using ICSharpCode.SharpZipLib.Checksums;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip.Compression;

namespace Couchbase.Lite.Internal
{

    // A class for parsing the changes received from the web socket changes
    // feed.  The changes are received in bursts one after another and are 
    // possibly compressed which makes for an interesting challenge.  The
    // client needs to continuously decompress different streams while treating
    // them as one big stream.  That is what this class and the ChunkStream
    // class exist for
    internal sealed class ChunkedChanges : IDisposable
    {

        #region Constants

        private static readonly string Tag = typeof(ChunkedChanges).Name;

        // This buffer size is fairly arbitrary because we can't wait for a buffer to be
        // filled since we need to be informed of the newest data immediately and we don't
        // know when the next piece is going to come.  However, I don't want to read byte
        // after byte one at a time because it feels wrong.  
        private const int BufferSize = 64; 

        #endregion

        #region Variables

        private readonly ChunkStream _innerStream;
        private readonly Inflater _inflater; // Due to internal buffering, GZipStream cannot be used
        private bool _disposed;
        private bool _readHeader;

        public event TypedEventHandler<ChunkedChanges, IDictionary<string, object>> ChunkFound;

        public event TypedEventHandler<ChunkedChanges, Exception> Finished;

        #endregion

        #region Constructors

        public ChunkedChanges(bool compressed)
        {
            _innerStream = new ChunkStream();
            if (compressed) {
                _inflater = new Inflater(true);
            }

            Task.Factory.StartNew(Process, TaskCreationOptions.LongRunning);
        }

        #endregion

        #region Public Methods

        public void AddData(IEnumerable<byte> data)
        {
            if (_disposed) {
                Log.To.ChangeTracker.E(Tag, "AddData called on disposed object, throwing...");
                throw new ObjectDisposedException("ChunkedGZipChanges");
            }

            var realized = data.ToArray();
            using (var stream = new MemoryStream(realized)) {
                ReadHeader(stream);
                stream.CopyTo(_innerStream);
            }
        }

        #endregion

        #region Private methods

        private void Process()
        {
            List<byte> parseBuffer = new List<byte>();
            var nestedCount = 0;
            var exception = default(Exception);
            var unzipBuffer = new byte[BufferSize * 3]; 
            var zipBuffer = new byte[BufferSize];
            var readBytes = _innerStream.Read(zipBuffer, 0, zipBuffer.Length);
            while (readBytes > 0) {
                // We got some more bytes from the stream!
                var decodedBytes = Decode(zipBuffer, readBytes, unzipBuffer, out exception);
                while (decodedBytes > 0) {
                    // We decoded some more bytes from the stream!
                    for (int i = 0; i < decodedBytes; i++) {
                        parseBuffer.Add(unzipBuffer[i]);
                        if (IsEnd(parseBuffer.Last())) {
                            if (--nestedCount == 0 && parseBuffer.Count > 0) {
                                // We have a complete JSON object or array ready for processing
                                var changes = Manager.GetObjectMapper().ReadValue<IList<object>>(parseBuffer);
                                foreach (var change in changes) {
                                    if (ChunkFound != null) {
                                        ChunkFound(this, change.AsDictionary<string, object>());
                                    }
                                }

                                parseBuffer.Clear();
                            }
                        } else if (IsStart(parseBuffer.Last())) {
                            // Begin an embedded array
                            nestedCount++;
                        }
                    }

                    decodedBytes = Decode(null, 0, unzipBuffer, out exception);
                } 

                if (decodedBytes == -1) {
                    // The decode process failed, unable to continue
                    break;
                }

                readBytes = _innerStream.Read(zipBuffer, 0, zipBuffer.Length);
            }

            if (Finished != null) {
                Finished(this, exception);
            }
        }

        private int Decode(byte[] input, int inputLength, byte[] output, out Exception exception)
        {
            exception = null;
            if (_inflater == null) {
                // This is a non compressed stream, so simply copy over the bytes to the output
                Array.Copy(input, output, input.Length);
                return input.Length;
            }

            try {
                // If the input is null, it means keep processing the current input
                // (it could have filled the last output and still not be done)
                // Otherwise, set the new input
                if(input != null) {
                    _inflater.SetInput(input, 0, inputLength);
                }

                return _inflater.Inflate(output);
            } catch(Exception e) {
                exception = e;
                Log.To.ChangeTracker.E(Tag, "Failed to read from changes feed, sending to callback...", e);
                return -1;
            }
        }

        private static bool IsEnd(byte nextChar) {
            return nextChar == ']';
        }

        private static bool IsStart(byte nextChar) {
            return nextChar == '[';
        }

        // From SharpZipLib GZipInputStream, slightly modified
        private void ReadHeader(MemoryStream stream) 
        {
            if (_readHeader || _inflater == null) {
                return;
            }

            // 1. Check the two magic bytes
            Crc32 headCRC = new Crc32();
            int magic = stream.ReadByte();

            if (magic < 0) {
                throw new GZipException("EOS reading GZIP header");
            }

            headCRC.Update(magic);
            if (magic != (GZipConstants.GZIP_MAGIC >> 8)) {
                throw new GZipException("Error GZIP header, first magic byte doesn't match");
            }
                
            magic = stream.ReadByte();

            if (magic < 0) {
                throw new GZipException("EOS reading GZIP header");
            }

            if (magic != (GZipConstants.GZIP_MAGIC & 0xFF)) {
                throw new GZipException("Error GZIP header,  second magic byte doesn't match");
            }

            headCRC.Update(magic);

            // 2. Check the compression type (must be 8)
            int compressionType = stream.ReadByte();

            if ( compressionType < 0 ) {
                throw new GZipException("EOS reading GZIP header");
            }

            if ( compressionType != 8 ) {
                throw new GZipException("Error GZIP header, data not in deflate format");
            }
            headCRC.Update(compressionType);

            // 3. Check the flags
            int flags = stream.ReadByte();
            if (flags < 0) {
                throw new GZipException("EOS reading GZIP header");
            }
            headCRC.Update(flags);

            /*    This flag byte is divided into individual bits as follows:

            bit 0   FTEXT
            bit 1   FHCRC
            bit 2   FEXTRA
            bit 3   FNAME
            bit 4   FCOMMENT
            bit 5   reserved
            bit 6   reserved
            bit 7   reserved
            */

            // 3.1 Check the reserved bits are zero

            if ((flags & 0xE0) != 0) {
                throw new GZipException("Reserved flag bits in GZIP header != 0");
            }

            // 4.-6. Skip the modification time, extra flags, and OS type
            for (int i=0; i< 6; i++) {
                int readByte = stream.ReadByte();
                if (readByte < 0) {
                    throw new GZipException("EOS reading GZIP header");
                }
                headCRC.Update(readByte);
            }

            // 7. Read extra field
            if ((flags & GZipConstants.FEXTRA) != 0) {

                // XLEN is total length of extra subfields, we will skip them all
                int len1, len2;
                len1 = stream.ReadByte();
                len2 = stream.ReadByte();
                if ((len1 < 0) || (len2 < 0)) {
                    throw new GZipException("EOS reading GZIP header");
                }

                int extraLen = (len2 << 8) | len1;      // gzip is LSB first
                for (int i = 0; i < extraLen;i++) {
                    int readByte = stream.ReadByte();
                    if (readByte < 0) 
                    {
                        throw new GZipException("EOS reading GZIP header");
                    }
                    headCRC.Update(readByte);
                }
            }

            // 8. Read file name
            if ((flags & GZipConstants.FNAME) != 0) {
                int readByte;
                while ( (readByte = stream.ReadByte()) > 0) {
                    headCRC.Update(readByte);
                }

                if (readByte < 0) {
                    throw new GZipException("EOS reading GZIP header");
                }
            }

            // 9. Read comment
            if ((flags & GZipConstants.FCOMMENT) != 0) {
                int readByte;
                while ( (readByte = stream.ReadByte()) > 0) {
                    headCRC.Update(readByte);
                }

                if (readByte < 0) {
                    throw new GZipException("EOS reading GZIP header");
                }
            }

            // 10. Read header CRC
            if ((flags & GZipConstants.FHCRC) != 0) {
                int tempByte;
                int crcval = stream.ReadByte();
                if (crcval < 0) {
                    throw new GZipException("EOS reading GZIP header");
                }

                tempByte = stream.ReadByte();
                if (tempByte < 0) {
                    throw new GZipException("EOS reading GZIP header");
                }

                crcval = (crcval << 8) | tempByte;
                if (crcval != ((int) headCRC.Value & 0xffff)) {
                    throw new GZipException("Header CRC value mismatch");
                }
            }

            _readHeader = true;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed) {
                _disposed = true;
                _innerStream.Flush();
                _innerStream.Dispose();
            }
        }

        #endregion
    }
}


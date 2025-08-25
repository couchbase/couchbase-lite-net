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
using Couchbase.Lite.Internal.Logging;

using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc;

internal sealed unsafe class BlobReadStream : Stream
{
    private const string Tag = nameof(BlobReadStream);

    private long _length = -1;
    private long _position;
    private C4ReadStreamWrapper? _readStream;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length
    {
        get {
            if(_length > -1) {
                return _length;
            }

            if(_readStream == null) {
                WriteLog.To.Database.W(Tag, "Native read stream is null, returning 0 length");
                return 0;
            }

            C4Error err;
            var retVal = NativeSafe.c4stream_getLength(_readStream, &err);
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

    public BlobReadStream(C4BlobStore *store, C4BlobKey key)
    {
        Debug.Assert(store != null);

        _readStream = LiteCoreBridge.CheckTyped(err => NativeSafe.c4blob_openReadStream(store, key, err));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _readStream?.Dispose();
        _readStream = null;
    }

    public override void Flush()
    {
        // No-op
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if(_readStream == null) {
            throw new InvalidOperationException(CouchbaseLiteErrorMessage.BlobReadStreamNotOpen);
        }

        var retVal = 0;
        LiteCoreBridge.Check(err => retVal = (int)NativeSafe.c4stream_read(_readStream, buffer, err));
        _position += retVal;
        return retVal;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_readStream == null) {
            throw new InvalidOperationException(CouchbaseLiteErrorMessage.BlobReadStreamNotOpen);
        }

        switch (origin) {
            case SeekOrigin.Begin when offset < 0 || offset > Length:
                throw new ArgumentOutOfRangeException(nameof(offset), $"The offset must be between 0 and {Length} for SeekOrigin.Begin");
            case SeekOrigin.Begin:
                LiteCoreBridge.Check(err => NativeSafe.c4stream_seek(_readStream, (ulong)offset, err));
                _position = offset;
                break;
            case SeekOrigin.Current:
            {
                var newPos = _position + offset;
                if(newPos < 0 || newPos > Length) {
                    throw new ArgumentOutOfRangeException(nameof(offset), 
                        $"The offset {offset} would result in an invalid position {newPos} (needs to be between 0 and {Length})");
                }

                LiteCoreBridge.Check(err => NativeSafe.c4stream_seek(_readStream, (ulong)newPos, err));
                _position = newPos;
                break;
            }
            case SeekOrigin.End:
            default:
            {
                var newPos = Length + offset;
                if(newPos < 0 || newPos > Length) {
                    throw new ArgumentOutOfRangeException(nameof(offset),
                        $"The offset {offset} would result in an invalid position {newPos} (needs to be between 0 and {Length})");
                }

                LiteCoreBridge.Check(err => NativeSafe.c4stream_seek(_readStream, (ulong)newPos, err));
                _position = newPos;
                break;
            }
        }

        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
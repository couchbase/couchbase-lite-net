//
//  BlobWriteStream.cs
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
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class BlobWriteStream : Stream
    {
        #region Variables

        private C4WriteStream* _writeStream;

        #endregion

        #region Properties

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public C4BlobKey Key { get; private set; }

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        #endregion

        #region Constructors

        public BlobWriteStream([NotNull]C4BlobStore* store)
        {
            Debug.Assert(store != null);

            _writeStream = (C4WriteStream*)LiteCoreBridge.Check(err => Native.c4blob_openWriteStream(store, err));
        }

        #endregion

        #region Overrides

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Native.c4stream_closeWriter(_writeStream);
            _writeStream = null;
        }

        public override void Flush()
        {
            Key = Native.c4stream_computeBlobKey(_writeStream);
            LiteCoreBridge.Check(err => Native.c4stream_install(_writeStream, null, err));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            LiteCoreBridge.Check(err => Native.c4stream_write(_writeStream, buffer, err));
        }

        #endregion
    }
}

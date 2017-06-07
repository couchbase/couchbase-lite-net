// 
// SslStreamFactory.cs
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite.DI;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Couchbase.Lite.Support
{
    internal sealed class SslStreamFactory : ISslStreamFactory
    {
        #region ISslStreamFactory

        public ISslStream Create(Stream inner)
        {
            return new SslStream(inner);
        }

        #endregion
    }

    internal sealed class SslStream : ISslStream
    {
        #region Variables

        private readonly StreamSocket _innerStream;

        #endregion

        #region Constructors

        public SslStream(Stream inner)
        {
            _innerStream = new StreamSocket();
        }

        #endregion

        #region ISslStream

        public Stream AsStream()
        {
            return new StreamWrapper(_innerStream);
        }

        public async Task ConnectAsync(string targetHost, ushort targetPort, X509CertificateCollection clientCertificates, 
            bool checkCertificateRevocation)
        {
            await _innerStream.ConnectAsync(new HostName(targetHost), targetPort.ToString());
            await _innerStream.UpgradeToSslAsync(SocketProtectionLevel.Tls12, new HostName(targetHost));
        }

        #endregion

        #region Nested

        private sealed class StreamWrapper : Stream
        {
            #region Variables

            private readonly StreamSocket _parent;
            private readonly DataWriter _writer;
            private readonly DataReader _reader;

            #endregion

            #region Properties

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            #endregion

            #region Constructors

            public StreamWrapper(StreamSocket parent)
            {
                _parent = parent;
                _writer = new DataWriter(_parent.OutputStream);
                _reader = new DataReader(_parent.InputStream) {
                    InputStreamOptions = InputStreamOptions.Partial
                };
            }

            #endregion

            #region Overrides

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                _parent.Dispose();
            }

            public override void Flush()
            {
                _parent.InputStream.AsStreamForRead().Flush();
                _parent.OutputStream.AsStreamForWrite().Flush();
            }

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                return _parent.InputStream.AsStreamForRead().CopyToAsync(destination, bufferSize, cancellationToken);
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return _parent.OutputStream.FlushAsync().AsTask(cancellationToken);
            }

            public override int ReadByte()
            {
                return _parent.InputStream.AsStreamForRead().ReadByte();
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var read = await _reader.LoadAsync((uint) count);
                var winrtBuffer = _reader.ReadBuffer(read);
                if (read > 0) {
                    winrtBuffer.CopyTo(0U, buffer, offset, (int) read);
                }

                return (int)read;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException("Sync reading not supported");
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
                throw new NotSupportedException("Sync writing not supported");
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                _writer.WriteBytes(buffer.Skip(offset).Take(count).ToArray());
                await _writer.StoreAsync();
            }

            #endregion
        }

        #endregion
    }
}

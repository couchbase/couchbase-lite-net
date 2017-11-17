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
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite.DI;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage.Streams;
using Couchbase.Lite.Logging;

namespace Couchbase.Lite.Support
{
    // HACK: This class is not available currently in UWP (System.Net.Security)
    internal sealed class AuthenticationException : Exception
    {
        #region Constructors

        public AuthenticationException() : base(
            "The remote certificate is invalid according to the validation procedure.")
        {
            
        }

        #endregion
    }

    [CouchbaseDependency(Lazy = true)]
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
        #region Constants

        private const string Tag = nameof(SslStream);

        #endregion

        #region Variables

        private readonly StreamSocket _innerStream;

        #endregion

        #region Properties

        public X509Certificate2 PinnedServerCertificate { get; set; }

        #endregion

        #region Constructors

        public SslStream(Stream inner)
        {
            _innerStream = new StreamSocket();
        }

        #endregion

        private bool VerifyCert()
        {
            var receivedCert = _innerStream.Information.ServerCertificate;
            if (PinnedServerCertificate == null) {
                return false;
            }

            var serverData = receivedCert.GetCertificateBlob().ToArray();
            if (serverData.SequenceEqual(PinnedServerCertificate.Export(X509ContentType.Cert))) {
                return true;
            }

            _innerStream.Dispose();
            Log.To.Sync.W(Tag, "Server certificate did not match the pinned one");
            throw new AuthenticationException();
        }

        #region ISslStream

        public Stream AsStream()
        {
            return new StreamWrapper(_innerStream);
        }

        public async Task ConnectAsync(string targetHost, ushort targetPort, X509CertificateCollection clientCertificates, 
            bool checkCertificateRevocation)
        {
            if (PinnedServerCertificate != null) {
                _innerStream.Control.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);
            }

            if (clientCertificates?.Count > 0) {
                var regularCert = clientCertificates[0];
                var data = regularCert.Export(X509ContentType.Pkcs7);
                _innerStream.Control.ClientCertificate = new Certificate(data.AsBuffer());
            }

            await _innerStream.ConnectAsync(new HostName(targetHost), targetPort.ToString());
            try {
                await _innerStream.UpgradeToSslAsync(SocketProtectionLevel.Tls12, new HostName(targetHost));
            } catch (Exception e) {
                if (VerifyCert()) {
                    return;
                }

                Log.To.Sync.W(Tag, "Exception while negotiating SSL connection", e);
                var rethrow = false;
                for (var i = 0; i < _innerStream.Information.ServerCertificateErrors.Count; i++) {
                    var err = _innerStream.Information.ServerCertificateErrors[i];
                    if (err == ChainValidationResult.Success) {
                        continue;
                    }

                    var cert = (i == _innerStream.Information.ServerCertificateErrors.Count - 1)
                        ? _innerStream.Information.ServerCertificate : _innerStream.Information.ServerIntermediateCertificates[i];
                    Log.To.Sync.V(Tag, $"Got result {err} for certificate: " +
                                       $"{Environment.NewLine}[SN]{cert.Subject} " +
                                       $"{Environment.NewLine}[Issuer]{cert.Issuer}" +
                                       $"{Environment.NewLine}[Serial]{BitConverter.ToString(cert.SerialNumber)}" +
                                       $"{Environment.NewLine}[Hash]{BitConverter.ToString(cert.GetHashValue())}");
                    if (err != ChainValidationResult.Untrusted) {
                        rethrow = true;
                    }
                }

                if (rethrow) {
                    throw;
                }

                throw new AuthenticationException();
            }

            VerifyCert();
        }

        #endregion

        #region Nested

        private sealed class StreamWrapper : Stream
        {
            #region Variables

            private readonly StreamSocket _parent;
            private readonly DataReader _reader;
            private readonly DataWriter _writer;

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

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                return _parent.InputStream.AsStreamForRead().CopyToAsync(destination, bufferSize, cancellationToken);
            }

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

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return _parent.OutputStream.FlushAsync().AsTask(cancellationToken);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException("Sync reading not supported");
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

            public override int ReadByte()
            {
                return _parent.InputStream.AsStreamForRead().ReadByte();
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

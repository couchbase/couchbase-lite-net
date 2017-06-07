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
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Couchbase.Lite.DI;

namespace Couchbase.Lite.Support
{
    internal sealed class SslStreamFactory : ISslStreamFactory
    {
        #region ISslStreamFactory

        public ISslStream Create(Stream inner)
        {
            return new SslStreamImpl(inner);
        }

        #endregion
    }

    internal sealed class SslStreamImpl : ISslStream
    {
        #region Variables

        private readonly SslStream _innerStream;

        #endregion

        #region Constructors

        public SslStreamImpl(Stream inner)
        {
            _innerStream = new SslStream(inner);
        }

        #endregion

        #region ISslStream

        public Stream AsStream()
        {
            return _innerStream;
        }

        public Task ConnectAsync(string targetHost, ushort targetPort, X509CertificateCollection clientCertificates,
            bool checkCertificateRevocation)
        {
            return _innerStream.AuthenticateAsClientAsync(targetHost, clientCertificates, SslProtocols.Tls12,
                checkCertificateRevocation);
        }

        #endregion
    }
}

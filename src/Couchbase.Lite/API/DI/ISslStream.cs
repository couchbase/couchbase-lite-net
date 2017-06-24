// 
// ISslStream.cs
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
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Couchbase.Lite.DI
{
    public sealed class SslException : Exception
    {
        internal SslException() : base("The server certificate was rejected")
        {
            
        }
    }

    /// <summary>
    /// WARNING: This interface is a temporary solution to https://github.com/dotnet/corefx/issues/19783
    /// and is not meant to be permanent.  Once UWP 6.0 is out, this interface will be removed.
    /// 
    /// This interface is designed to create an <see cref="ISslStream"/> for use in writing data over
    /// a TLS encrypted TCP connection.  Normally, we could just use <c>System.Net.Security.SslStream</c>
    /// but UWP does not provide an implementation of that!
    /// </summary>
    public interface ISslStreamFactory
    {
        #region Public Methods

        /// <summary>
        /// Creates a stream for reading and writing
        /// TLS encrypted data over TCP
        /// </summary>
        /// <param name="inner">The underlying </param>
        /// <returns>The instantiated stream ready for communication</returns>
        ISslStream Create(Stream inner);

        #endregion
    }

    /// <summary>
    /// An interface of an abstract object which transports data over
    /// a TLS encrypted stream
    /// </summary>
    public interface ISslStream
    {

        #region Properties

        /// <summary>
        /// Gets or sets the certificate to use for server validation.  All other
        /// certificates will be rejected
        /// </summary>
        X509Certificate2 PinnedServerCertificate { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets this object as a <see cref="Stream"/> for reading and/or writing
        /// </summary>
        /// <returns>The object as a stream</returns>
        Stream AsStream();

        /// <summary>
        /// Begins the process of connecting to the remote host and negotiating the TLS
        /// handshake
        /// </summary>
        /// <param name="targetHost">The host name or IP to connect to</param>
        /// <param name="targetPort">The TCP host port to connect to</param>
        /// <param name="clientCertificates">The client certificates to use, if any (note: still not implemented)</param>
        /// <param name="checkCertificateRevocation">Whether or not to check for certificate revocation (not supported on UWP)</param>
        /// <returns>An awaitable <see cref="Task"/></returns>
        Task ConnectAsync(string targetHost, ushort targetPort, X509CertificateCollection clientCertificates,
            bool checkCertificateRevocation);

        #endregion
    }
}

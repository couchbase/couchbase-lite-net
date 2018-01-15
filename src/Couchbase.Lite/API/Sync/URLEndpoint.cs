// 
//  URLEndpoint.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// Represents a remote endpoint for a <see cref="Replicator"/>
    /// </summary>
    public sealed class URLEndpoint : IEndpoint
    {
        #region Constants

        private const string Tag = nameof(URLEndpoint);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the hostname for this endpoint
        /// </summary>
        [NotNull]
        public string Host { get; }

        /// <summary>
        /// Gets the path into the hostname used for this endpoint, if any
        /// </summary>
        [CanBeNull]
        public string Path { get; }

        /// <summary>
        /// Gets the port used for this endpoint
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets whether or not this endpoint uses TLS
        /// </summary>
        public bool Secure { get; }

        [NotNull]
        internal Uri Url { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs an endpoint given a remote host and whether or not to use TLS,
        /// using the default port 4984
        /// </summary>
        /// <param name="host">The host to connect to</param>
        /// <param name="secure">Whether or not to use TLS (server must be configured for TLS)</param>
        public URLEndpoint([NotNull]string host, bool secure)
        {
            var builder = new UriBuilder(secure ? "blips" : "blip", host, 4984);
            Host = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(host), host);
            Port = 4984;
            Secure = secure;
            Url = builder.Uri;
        }

        /// <summary>
        /// Constructs an endpoint given a remote host base, whether or not to use TLS,
        /// and a path into the remote host using the default port 4984
        /// </summary>
        /// <param name="host">The host to connect to</param>
        /// <param name="path">The  path into the remote host</param>
        /// <param name="secure">Whether or not to use TLS (server must be configured for TLS)</param>
        public URLEndpoint([NotNull]string host, [CanBeNull]string path, bool secure)
        {
            var builder = new UriBuilder(secure ? "blips" : "blip", host, 4984, path);
            Host = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(host), host);
            Port = 4984;
            Path = path;
            Secure = secure;
            Url = builder.Uri;
        }

        /// <summary>
        /// Constructs an endpoint given a remote host and whether or not to use TLS,
        /// using the given port
        /// </summary>
        /// <param name="host">The host to connect to</param>
        /// <param name="port">The port to use when connecting</param>
        /// <param name="secure">Whether or not to use TLS (server must be configured for TLS)</param>
        public URLEndpoint([NotNull]string host, int port, bool secure)
        {
            var builder = new UriBuilder(secure ? "blips" : "blip", host, port);
            Host = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(host), host);
            Port = port;
            Secure = secure;
            Url = builder.Uri;
        }

        /// <summary>
        /// Constructs an endpoint given a remote host base, whether or not to use TLS,
        /// and a path into the remote host using the given port
        /// </summary>
        /// <param name="host">The host to connect to</param>
        /// <param name="port">The port to use when connecting</param>
        /// <param name="path">The  path into the remote host</param>
        /// <param name="secure">Whether or not to use TLS (server must be configured for TLS)</param>
        public URLEndpoint([NotNull]string host, int port, [CanBeNull]string path, bool secure)
        {
            var builder = new UriBuilder(secure ? "blips" : "blip", host, port, path);
            Host = CBDebug.MustNotBeNull(Log.To.Sync, Tag, nameof(host), host);
            Port = port;
            Path = path;
            Secure = secure;
            Url = builder.Uri;
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override string ToString() => Url.ToString();

        #endregion
    }
}
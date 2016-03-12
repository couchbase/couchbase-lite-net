//
// ReplicationOptions.cs
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
using Couchbase.Lite.Util;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class for specifying options applicable to replication
    /// </summary>
    public sealed class ReplicationOptions
    {
        /// <summary>
        /// The default value for Heartbeat (5 minutes)
        /// </summary>
        public static readonly TimeSpan DefaultHeartbeat = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The default value for RequestTimeout (60 seconds)
        /// </summary>
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// The default value for SocketTimeout (5 minutes)
        /// </summary>
        public static readonly TimeSpan DefaultSocketTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The default value for MaxRetries (2)
        /// </summary>
        public static readonly int DefaultMaxRetries = 2;

        /// <summary>
        /// The default value for MaxOpenHttpConnections (8)
        /// </summary>
        public static readonly int DefaultMaxOpenHttpConnections = 8;

        /// <summary>
        /// The default value for MaxRevsToGetInBulk (50)
        /// </summary>
        public static readonly int DefaultMaxRevsToGetInBulk = 50;

        /// <summary>
        /// Gets or sets whether or not the replication should forcibly start over
        /// (getting changes from the beginning of time)
        /// </summary>
        public bool Reset { get; set; }

        /// <summary>
        /// Gets or sets the request timeout for requests during the 
        /// replication process (i.e. if the server takes longer than
        /// X to respond to a request, it is considered timed out)
        /// </summary>
        public TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// Gets or sets the socket timeout for requests during
        /// the replication process (i.e. if the client cannot
        /// read data from the server response for longer than
        /// X it is considered timed out)
        /// </summary>
        /// <value>The socket timeout.</value>
        public TimeSpan SocketTimeout { get; set; }

        /// <summary>
        /// Gets or sets the heartbeat value to specify when making
        /// long running requests (if honored by the server, a heartbeat
        /// message should be received every X amount of time and thus
        /// prevent closing the connection for being idle).  Must be
        /// >= 15 seconds.
        /// </summary>
        public TimeSpan Heartbeat
        {
            get { return _heartbeat; }
            set { 
                if (value < TimeSpan.FromSeconds(15)) {
                    Log.To.NoDomain.E("ReplicationOptions", "Invalid heartbeat specified ({0}), must be" +
                    " >= 15 seconds", value);
                    throw new ArgumentOutOfRangeException("value", value, "Must be >= 15 seconds");
                }

                _heartbeat = value;
            }
        }
        private TimeSpan _heartbeat;

        /// <summary>
        /// Gets or sets the time to wait between subsequent requests
        /// for changes (to throttle the amount of requests going to the
        /// server).  This is only applicable to continuous replications.
        /// </summary>
        public TimeSpan PollInterval { get; set; }

        /// <summary>
        /// Gets or sets whether or not web sockets are allowed during the replication
        /// process (default is *true*, set to false to disable web sockets)
        /// </summary>
        public bool UseWebSocket { get; set; }

        /// <summary>
        /// Gets or sets a value to identify the remote endpoint of a replication.
        /// This is useful in cases where the remote URL is subject to frequent 
        /// change (for example in a P2P discovery scenario) because it will replace
        /// the URL as part of the checkpoint calculation.  Otherwise, if the remote
        /// URL changes the replication is considered new and starts from the beginning
        /// of time
        /// </summary>
        public string RemoteUUID { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of times to retry a request that received
        /// a non-fatal (i.e. transient) error before giving up.  Only applicable to
        /// non-continuous requests (continuous requests will never give up)
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of parallel HTTP requests to make during
        /// replication
        /// </summary>
        public int MaxOpenHttpConnections { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of revisions to request in one bulk GET operation.
        /// Higher values will result in longer running requests with more data, while lower
        /// values will result in a higher number of requests that finish more quickly.  
        /// </summary>
        public int MaxRevsToGetInBulk { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public ReplicationOptions()
        {
            Heartbeat = DefaultHeartbeat;
            RequestTimeout = DefaultRequestTimeout;
            SocketTimeout = DefaultSocketTimeout;
            PollInterval = TimeSpan.Zero;
            UseWebSocket = true;
            MaxRetries = DefaultMaxRetries;
            MaxOpenHttpConnections = DefaultMaxOpenHttpConnections;
            MaxRevsToGetInBulk = DefaultMaxRevsToGetInBulk;
        }

        public override string ToString()
        {
            return string.Format("ReplicationOptions[Reset={0}, RequestTimeout={1}, SocketTimeout={2}, Heartbeat={3}, PollInterval={4}, UseWebSocket={5}, RemoteUUID={6}, MaxRetries={7}, MaxOpenHttpConnections={8}, MaxRevsToGetInBulk={9}]", Reset, RequestTimeout, SocketTimeout, Heartbeat, PollInterval, UseWebSocket, RemoteUUID, MaxRetries, MaxOpenHttpConnections, MaxRevsToGetInBulk);
        }
    }
}


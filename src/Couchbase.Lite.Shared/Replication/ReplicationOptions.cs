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
using System.Collections.Generic;
using Couchbase.Lite.Util;

namespace Couchbase.Lite
{
    /// <summary>
    /// A class for specifying options applicable to replication
    /// </summary>
    public sealed class ReplicationOptions
    {

        #region Constants

        private const string Tag = nameof(ReplicationOptions);

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
        /// The default value for MaxOpenHttpConnections (8)
        /// </summary>
        public static readonly int DefaultMaxOpenHttpConnections = 8;

        /// <summary>
        /// The default value for MaxRevsToGetInBulk (50)
        /// </summary>
        public static readonly int DefaultMaxRevsToGetInBulk = 50;

        /// <summary>
        /// The default RetryStrategy for replications (exponential backoff)
        /// </summary>
        public static readonly IRetryStrategy DefaultRetryStrategy = new ExponentialBackoffStrategy(2);

        /// <summary>
        /// The default value for ReplicationRetryDelay (60 seconds)
        /// </summary>
        public static readonly TimeSpan DefaultReplicationRetryDelay = TimeSpan.FromSeconds(60);

        #endregion

        #region Properties

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
        /// Gets or set the strategy to use when retrying requests that fail with
        /// a transient error (default is exponential backoff)
        /// </summary>
        public IRetryStrategy RetryStrategy { get; set; }

        /// <summary>
        /// Non-continuous replications will give up on revisions after a while, and
        /// restart the replication process to try to get them later.  This property
        /// will set how long they should wait before doing so.
        /// </summary>
        public TimeSpan ReplicationRetryDelay { get; set; }

        /// <summary>
        /// If <c>true</c>, purge documents after pushing
        /// </summary>
        public bool PurgePushed { get; set; }

        /// <summary>
        /// If <c>true</c>, assume all document revisions are new and skip asking
        /// the server if they exist during replication
        /// </summary>
        public bool AllNew { get; set; }

        #endregion

        #region Constructors

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
            MaxOpenHttpConnections = DefaultMaxOpenHttpConnections;
            MaxRevsToGetInBulk = DefaultMaxRevsToGetInBulk;
            RetryStrategy = DefaultRetryStrategy.Copy();
            ReplicationRetryDelay = DefaultReplicationRetryDelay;
        }

        internal ReplicationOptions(IDictionary<string, object> dictionary)
            : this()
        {
            long heartbeatMs;
            if(dictionary.TryGetValue<long>("heartbeat", out heartbeatMs)) {
                Heartbeat = TimeSpan.FromMilliseconds(heartbeatMs);
            }

            long requestTimeoutMs;
            if(dictionary.TryGetValue<long>("connection_timeout", out requestTimeoutMs)) {
                RequestTimeout = TimeSpan.FromMilliseconds(requestTimeoutMs);
            }

            long pollIntervalMs;
            if(dictionary.TryGetValue<long>("poll", out pollIntervalMs)) {
                if(pollIntervalMs >= 30000) {
                    PollInterval = TimeSpan.FromMilliseconds(pollIntervalMs);
                } else {
                    Log.To.Sync.W(Tag, $"poll interval of {pollIntervalMs} seconds is too short!");
                }
            }

            UseWebSocket = dictionary.GetCast<bool>("websocket");
            RemoteUUID = dictionary.GetCast<string>("remoteUUID");
            PurgePushed = dictionary.GetCast<bool>("purgePushed");
            AllNew = dictionary.GetCast<bool>("allNew");
            Reset = dictionary.GetCast<bool>("reset");
        }

        #endregion

        #region Overrides
#pragma warning disable 1591

        public override string ToString()
        {
            return string.Format("ReplicationOptions[Reset={0}, RequestTimeout={1}, SocketTimeout={2}, Heartbeat={3}, PollInterval={4}, UseWebSocket={5}, RemoteUUID={6}, MaxOpenHttpConnections={7}, MaxRevsToGetInBulk={8}, RetryStrategy={9}, ReplicationRetryDelay={10}]", Reset, RequestTimeout, SocketTimeout, Heartbeat, PollInterval, UseWebSocket, RemoteUUID, MaxOpenHttpConnections, MaxRevsToGetInBulk, RetryStrategy, ReplicationRetryDelay);
        }

#pragma warning restore 1591
        #endregion

    }
}


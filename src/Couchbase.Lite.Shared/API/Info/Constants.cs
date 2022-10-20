//  Constants.cs
//
//  Copyright (c) 2022 Couchbase, Inc All rights reserved.
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
using Couchbase.Lite.Sync;
using Couchbase.Lite.Query;
using Couchbase.Lite.Logging;
#if COUCHBASE_ENTERPRISE
using Couchbase.Lite.P2P;
#endif

using System;

namespace Couchbase.Lite.Info
{
    public static class Constants
    {
        #if COUCHBASE_ENTERPRISE
        #region Constants in URLEndpointListenerConfiguration

        /// <summary>
        /// Default port <see cref="URLEndpointListenerConfiguration.Port" /> property value in URLEndpointListenerConfiguration.
        /// The default value is zero which means that the listener will automatically select an available port to listen to when the listener is started.
        /// </summary>
        public static readonly ushort DefaultListenerPort = 0;

        /// <summary>
        /// Default DisableTLS <see cref="URLEndpointListenerConfiguration.DisableTLS" /> property value in URLEndpointListenerConfiguration.
        /// The default value is <c>false</c> which means that the TLS will be enabled by default.
        /// </summary>
        public static readonly bool DefaultListenerDisableTLS = false;

        /// <summary>
        /// Default ReadOnly <see cref="URLEndpointListenerConfiguration.ReadOnly" /> property value in URLEndpointListenerConfiguration.
        /// The default value is <c>false</c> which means both push and pull replications are allowed to/from the listener.
        /// </summary>
        public static readonly bool DefaultListenerReadOnly = false;

        /// <summary>
        /// Default EnableDeltaSync <see cref="URLEndpointListenerConfiguration.EnableDeltaSync" /> property value in URLEndpointListenerConfiguration.
        /// The default value is <c>false</c> which means delta sync is diabled in replicating with the listener.
        /// </summary>
        public static readonly bool DefaultListenerEnableDeltaSync = false;

        #endregion
        #endif

        #region Constants in ReplicatorConfiguration

        /// <summary>
        /// Default ReplicatorType <see cref="ReplicatorConfiguration.ReplicatorType" /> property value in ReplicatorConfiguration.
        /// The default is <see cref="ReplicatorType.PushAndPull"/> which is bidirectional
        /// </summary>
        public static readonly ReplicatorType DefaultReplicatorType = ReplicatorType.PushAndPull;

        /// <summary>
        /// Default Continuous <see cref="ReplicatorConfiguration.Continuous" /> property value in ReplicatorConfiguration.
        /// The default value is <c>false</c> which means <see cref="Replicator"/> is not stay active indefinitely.
        /// </summary>
        public static readonly bool DefaultReplicatorContinuous = false;

        /// <summary>
        /// Default Heartbeat <see cref="ReplicatorConfiguration.Heartbeat" /> property value in ReplicatorConfiguration.
        /// The replicator heartbeat keep-alive interval default is 5 min.
        /// </summary>
        public static readonly TimeSpan DefaultReplicatorHeartbeat = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Default MaxAttempts <see cref="ReplicatorConfiguration.MaxAttempts" /> property value in Single Shot ReplicatorConfiguration.
        /// The default value is 9 max number of retry attempts to connect to peer in a single shot replication.
        /// </summary>
        public static readonly int DefaultReplicatorMaxAttemptsSingleShot = 9;

        /// <summary>
        /// Default MaxAttempts <see cref="ReplicatorConfiguration.MaxAttempts" /> property value in Continuous ReplicatorConfiguration.
        /// The default value is <see cref="int.MaxValue" /> max number of retry attempts to connect to peer in a continuous replication.
        /// </summary>
        public static readonly int DefaultReplicatorMaxAttemptsContinuous = int.MaxValue;

        /// <summary>
        /// Default MaxAttemptsWaitTime <see cref="ReplicatorConfiguration.MaxAttemptsWaitTime" /> property value in ReplicatorConfiguration.
        /// The max delay between retries default value is 5 min interval.
        /// </summary>
        public static readonly TimeSpan DefaultReplicatorMaxAttemptsWaitTime = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Default EnableAutoPurge <see cref="ReplicatorConfiguration.EnableAutoPurge" /> property value in ReplicatorConfiguration.
        /// The default value is <c>true</c> which means that the document will be automatically purged by the pull replicator when 
        /// the user loses access to the document from both removed and revoked scenarios.
        /// </summary>
        public static readonly bool DefaultReplicatorEnableAutoPurge = true;

        #endregion

        #region Constants in FullTextIndexConfiguration

        /// <summary>
        /// Default IgnoreAccents <see cref="FullTextIndexConfiguration.IgnoreAccents" /> property value in FullTextIndexConfiguration.
        /// The default value is <c>false</c> which means not to ignore accents when performing the full text search.
        /// </summary>
        public static readonly bool DefaultFullTextIndexIgnoreAccents = false;

        #endregion

        #region Constants in LogFileConfiguration

        /// <summary>
        /// Default UsePlaintext <see cref="LogFileConfiguration.UsePlaintext" /> property value in LogFileConfiguration.
        /// The default is to log in a binary encoded format that is more CPU and I/O friendly and enabling plaintext is 
        /// not recommended in production.
        /// </summary>
        public static readonly bool DefaultLogFileUsePlainText = false;

        /// <summary>
        /// Default MaxSize <see cref="LogFileConfiguration.MaxSize" /> property value in LogFileConfiguration.
        /// The default max size of the log files is 512 * 1024 (524288) bytes.
        /// </summary>
        public static readonly long DefaultLogFileMaxSize = 512 * 1024;

        /// <summary>
        /// Default MaxRotateCount <see cref="LogFileConfiguration.MaxRotateCount" /> property value in LogFileConfiguration.
        /// The default number of rotated logs that are saved is 1. 
        /// If the value is 1, then 2 logs will be present: the 'current' and the 'rotated'
        /// </summary>
        public static readonly uint DefaultLogFileMaxRotateCount = 1;

        #endregion
    }
}

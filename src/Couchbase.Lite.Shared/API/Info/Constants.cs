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
using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Info
{
    internal class Constants
    {
        // URLEndpointListenerConfiguration
        internal const ushort DefaultListenerPort = 0;
        internal const bool DefaultListenerDisableTLS = false;
        internal const bool DefaultListenerReadOnly = false;
        internal const bool DefaultListenerEnableDeltaSync = false;

        // ReplicatorConfiguration
        internal const ReplicatorType DefaultReplicatorType = ReplicatorType.PushAndPull;
        internal const bool DefaultReplicatorContinuous = false;
        internal const long DefaultReplicatorHeartbeat = 300; //seconds
        internal const int DefaultReplicatorMaxAttemptsSingleShot = 9;
        internal const int DefaultReplicatorMaxAttemptsContinuous = int.MaxValue;
        internal const long DefaultReplicatorMaxAttemptWaitTime = 300; //seconds
        internal const bool DefaultReplicatorEnableAutoPurge = true;

        // FullTextIndexConfiguration
        internal const bool DefaultFullTextIndexIgnoreAccents = false;

        // LogFileConfiguration
        internal const bool DefaultLogFileUsePlainText = false;
        internal const long DefaultLogFileMaxSize = 52428;
        internal const int DefaultLogFileMaxRotateCount = 1;
    }
}

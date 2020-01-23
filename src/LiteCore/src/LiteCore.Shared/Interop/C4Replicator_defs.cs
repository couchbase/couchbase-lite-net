//
// C4Replicator_defs.cs
//
// Copyright (c) 2020 Couchbase, Inc All rights reserved.
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using LiteCore.Util;

namespace LiteCore.Interop
{
    internal enum C4ReplicatorMode : int
    {
        Disabled,
        Passive,
        OneShot,
        Continuous
    }

    internal enum C4ReplicatorActivityLevel : int
    {
        Stopped,
        Offline,
        Connecting,
        Idle,
        Busy
    }

    [Flags]
    internal enum C4ReplicatorStatusFlags : int
    {
        WillRetry     = 0x1,
        HostReachable = 0x2,
        Suspended     = 0x4
    }

	internal unsafe partial struct C4Address
    {
        public FLSlice scheme;
        public FLSlice hostname;
        public ushort port;
        public FLSlice path;
    }

	internal unsafe struct C4Progress
    {
        public ulong unitsCompleted;
        public ulong unitsTotal;
        public ulong documentCount;
    }

	internal unsafe struct C4ReplicatorStatus
    {
        public C4ReplicatorActivityLevel level;
        public C4Progress progress;
        public C4Error error;
        public C4ReplicatorStatusFlags flags;
    }

	internal unsafe struct C4DocumentEnded
    {
        public FLHeapSlice docID;
        public FLHeapSlice revID;
        public C4RevisionFlags flags;
        public ulong sequence;
        public C4Error error;
        private byte _errorIsTransient;

        public bool errorIsTransient
        {
            get {
                return Convert.ToBoolean(_errorIsTransient);
            }
            set {
                _errorIsTransient = Convert.ToByte(value);
            }
        }
    }

    internal unsafe partial struct C4ReplicatorParameters
    {
        public C4ReplicatorMode push;
        public C4ReplicatorMode pull;
        public FLSlice optionsDictFleece;
        public IntPtr pushFilter;
        public IntPtr validationFunc;
        public IntPtr onStatusChanged;
        public IntPtr onDocumentEnded;
        public IntPtr onBlobProgress;
        public void* callbackContext;
        public C4SocketFactory* socketFactory;
    }

}
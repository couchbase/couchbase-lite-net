//
// C4Socket_defs.cs
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
    internal enum C4WebSocketCloseCode : int
    {
        WebSocketCloseNormal           = 1000,
        WebSocketCloseGoingAway        = 1001,
        WebSocketCloseProtocolError    = 1002,
        WebSocketCloseDataError        = 1003,
        WebSocketCloseNoCode           = 1005,
        WebSocketCloseAbnormal         = 1006,
        WebSocketCloseBadMessageFormat = 1007,
        WebSocketClosePolicyError      = 1008,
        WebSocketCloseMessageTooBig    = 1009,
        WebSocketCloseMissingExtension = 1010,
        WebSocketCloseCantFulfill      = 1011,
        WebSocketCloseTLSFailure       = 1015,
        WebSocketCloseAppTransient     = 4001,
        WebSocketCloseAppPermanent     = 4002,
        WebSocketCloseFirstAvailable   = 5000,
    }

    internal enum C4SocketFraming : byte
    {
        WebSocketClientFraming,
        NoFraming,
        WebSocketServerFraming,
    }

	internal unsafe struct C4Socket
    {
        public void* nativeHandle;
    }

    internal unsafe struct C4SocketFactory
    {
        public C4SocketFraming framing;
        public void* context;
        public IntPtr open;
        public IntPtr write;
        public IntPtr completedReceive;
        public IntPtr close;
        public IntPtr requestClose;
        public IntPtr dispose;
    }

}
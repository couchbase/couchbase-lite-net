//
// C4Listener_defs.cs
//
// Copyright (c) 2018 Couchbase, Inc All rights reserved.
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
    [Flags]
#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum C4ListenerAPIs : uint
    {
        RESTAPI = 0x01,
        SyncAPI = 0x02
    }

	internal unsafe struct C4Listener
    {
    }

	internal unsafe struct C4ListenerConfig
    {
        public ushort port;
        public C4ListenerAPIs apis;
        public FLSlice directory;
        private byte _allowCreateDBs;
        private byte _allowDeleteDBs;
        private byte _allowPush;
        private byte _allowPull;

        public bool allowCreateDBs
        {
            get {
                return Convert.ToBoolean(_allowCreateDBs);
            }
            set {
                _allowCreateDBs = Convert.ToByte(value);
            }
        }

        public bool allowDeleteDBs
        {
            get {
                return Convert.ToBoolean(_allowDeleteDBs);
            }
            set {
                _allowDeleteDBs = Convert.ToByte(value);
            }
        }

        public bool allowPush
        {
            get {
                return Convert.ToBoolean(_allowPush);
            }
            set {
                _allowPush = Convert.ToByte(value);
            }
        }

        public bool allowPull
        {
            get {
                return Convert.ToBoolean(_allowPull);
            }
            set {
                _allowPull = Convert.ToByte(value);
            }
        }
    }
}
//
// C4Listener_defs.cs
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
    [Flags]
    internal enum C4ListenerAPIs : uint
    {
        RESTAPI = 0x01,
        SyncAPI = 0x02
    }

    internal enum C4PrivateKeyRepresentation : uint
    {
        PrivateKeyFromCert,
        PrivateKeyFromKey,
    }

	internal unsafe struct C4TLSConfig
    {
        public C4PrivateKeyRepresentation privateKeyRepresentation;
        public C4KeyPair* key;
        public C4Cert* certificate;
        private byte _requireClientCerts;
        public C4Cert* rootClientCerts;
        public IntPtr certAuthCallback;
        public void* tlsCallbackContext;

        public bool requireClientCerts
        {
            get {
                return Convert.ToBoolean(_requireClientCerts);
            }
            set {
                _requireClientCerts = Convert.ToByte(value);
            }
        }
    }

	internal unsafe struct C4ListenerConfig
    {
        public ushort port;
        public FLSlice networkInterface;
        public C4ListenerAPIs apis;
        public C4TLSConfig* tlsConfig;
        public IntPtr httpAuthCallback;
        public void* callbackContext;
        public FLSlice directory;
        private byte _allowCreateDBs;
        private byte _allowDeleteDBs;
        private byte _allowPush;
        private byte _allowPull;
        private byte _enableDeltaSync;

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

        public bool enableDeltaSync
        {
            get {
                return Convert.ToBoolean(_enableDeltaSync);
            }
            set {
                _enableDeltaSync = Convert.ToByte(value);
            }
        }
    }
}
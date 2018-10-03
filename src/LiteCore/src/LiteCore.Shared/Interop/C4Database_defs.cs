//
// C4Database_defs.cs
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
    enum C4DatabaseFlags : uint
    {
        Create        = 1,
        ReadOnly      = 2,
        AutoCompact   = 4,
        SharedKeys    = 0x10,
        NoUpgrade     = 0x20,
        NonObservable = 0x40,
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum C4EncryptionAlgorithm : uint
    {
        None = 0,
        AES256,
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum C4DocumentVersioning : uint
    {
        RevisionTrees,
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum C4EncryptionKeySize : ulong
    {
        KeySizeAES256 = 32,
    }

	internal unsafe partial struct C4DatabaseConfig
    {
        public C4DatabaseFlags flags;
        private IntPtr _storageEngine;
        public C4DocumentVersioning versioning;
        public C4EncryptionKey encryptionKey;

        public string storageEngine
        {
            get {
                return Marshal.PtrToStringAnsi(_storageEngine);
            }
            set {
                var old = Interlocked.Exchange(ref _storageEngine, Marshal.StringToHGlobalAnsi(value));
                Marshal.FreeHGlobal(old);
            }
        }
    }

	internal unsafe partial struct C4UUID
    {
        public fixed byte bytes[16];
    }

	internal unsafe struct C4RawDocument
    {
        public FLSlice key;
        public FLSlice meta;
        public FLSlice body;
    }

	internal unsafe partial struct C4EncryptionKey
    {
        public C4EncryptionAlgorithm algorithm;
        public fixed byte bytes[32];
    }

	internal unsafe struct C4Database
    {
    }
}
//
// C4DatabaseTypes_defs.cs
//
// Copyright (c) 2022 Couchbase, Inc All rights reserved.
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
    internal enum C4DatabaseFlags : uint
    {
        Create        = 0x01,
        ReadOnly      = 0x02,
        AutoCompact   = 0x04,
        VersionVectors= 0x08,
        NoUpgrade     = 0x20,
        NonObservable = 0x40,
    }

    internal enum C4EncryptionAlgorithm : uint
    {
        None = 0,
        AES256,
    }

    internal enum C4EncryptionKeySize : ulong
    {
        KeySizeAES256 = 32,
    }

    internal enum C4MaintenanceType : uint
    {
        Compact,
        Reindex,
        IntegrityCheck,
        QuickOptimize,
        FullOptimize,
    }

	internal unsafe partial struct C4EncryptionKey
    {
        public C4EncryptionAlgorithm algorithm;
        public fixed byte bytes[32];
    }

	internal unsafe partial struct C4DatabaseConfig2
    {
        public FLSlice parentDirectory;
        public C4DatabaseFlags flags;
        public C4EncryptionKey encryptionKey;
    }

	internal unsafe partial struct C4UUID
    {
        public fixed byte bytes[16];
    }

	internal unsafe struct C4CollectionSpec
    {
        public FLSlice name;
        public FLSlice scope;
    }

	internal unsafe struct C4RawDocument
    {
        public FLSlice key;
        public FLSlice meta;
        public FLSlice body;
    }
}
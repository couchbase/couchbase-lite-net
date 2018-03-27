//
// C4DocEnumerator_defs.cs
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
    enum C4EnumeratorFlags : ushort
    {
        Descending           = 0x01,
        IncludeDeleted       = 0x08,
        IncludeNonConflicted = 0x10,
        IncludeBodies        = 0x20
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe partial struct C4EnumeratorOptions
    {
        public C4EnumeratorFlags flags;
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe struct C4DocumentInfo
    {
        public C4DocumentFlags flags;
        public C4Slice docID;
        public C4Slice revID;
        public ulong sequence;
        public ulong bodySize;
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe struct C4DocEnumerator
    {
    }
}
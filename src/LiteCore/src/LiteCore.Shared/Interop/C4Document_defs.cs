//
// C4Document_defs.cs
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
    internal enum C4DocumentFlags : uint
    {
        DocDeleted         = 0x01,
        DocConflicted      = 0x02,
        DocHasAttachments  = 0x04,
        DocExists          = 0x1000
    }

    [Flags]
    internal enum C4RevisionFlags : byte
    {
        Deleted        = 0x01,
        Leaf           = 0x02,
        New            = 0x04,
        HasAttachments = 0x08,
        KeepBody       = 0x10,
        IsConflict     = 0x20,
        Closed         = 0x40,
        Purged         = 0x80,
    }

	internal unsafe struct C4Revision
    {
        public FLHeapSlice revID;
        public C4RevisionFlags flags;
        public ulong sequence;
        public FLSlice body;
    }

	internal unsafe partial struct C4Document
    {
        public C4DocumentFlags flags;
        public FLHeapSlice docID;
        public FLHeapSlice revID;
        public ulong sequence;
        public C4Revision selectedRev;
        public C4ExtraInfo extraInfo;
    }

	internal unsafe partial struct C4DocPutRequest
    {
        public FLSlice body;
        public FLSlice docID;
        public C4RevisionFlags revFlags;
        private byte _existingRevision;
        private byte _allowConflict;
        public FLSlice* history;
        private UIntPtr _historyCount;
        private byte _save;
        public uint maxRevTreeDepth;
        public uint remoteDBID;
        public FLSliceResult allocedBody;
        private IntPtr _deltaCB;
        public void* deltaCBContext;
        public FLSlice deltaSourceRevID;

        public bool existingRevision
        {
            get {
                return Convert.ToBoolean(_existingRevision);
            }
            set {
                _existingRevision = Convert.ToByte(value);
            }
        }

        public bool allowConflict
        {
            get {
                return Convert.ToBoolean(_allowConflict);
            }
            set {
                _allowConflict = Convert.ToByte(value);
            }
        }

        public ulong historyCount
        {
            get {
                return _historyCount.ToUInt64();
            }
            set {
                _historyCount = (UIntPtr)value;
            }
        }

        public bool save
        {
            get {
                return Convert.ToBoolean(_save);
            }
            set {
                _save = Convert.ToByte(value);
            }
        }

        public C4DocDeltaApplier deltaCB
        {
            get {
                return  Marshal.GetDelegateForFunctionPointer<C4DocDeltaApplier>(_deltaCB);
            }
            set {
                _deltaCB = Marshal.GetFunctionPointerForDelegate(value);
            }
        }
    }
}
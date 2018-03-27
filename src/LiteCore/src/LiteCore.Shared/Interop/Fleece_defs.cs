//
// Fleece_defs.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
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
#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum FLEncoderFormat
    {
        EncodeFleece,
        EncodeJSON,
        EncodeJSON5
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum FLValueType
    {
        Undefined = -1,
        Null = 0,
        Boolean,
        Number,
        String,
        Data,
        Array,
        Dict
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    enum FLError
    {
        NoError = 0,
        MemoryError,
        OutOfRange,
        InvalidData,
        EncodeError,
        JSONError,
        UnknownValue,
        InternalError,
        NotFound,
        SharedKeysStateError,
    }

    internal unsafe struct FLDictIterator
    {
        #pragma warning disable CS0169

        private void* _private1;
        private uint _private2;
        private byte _private3;

        // _private4[3]
        private void* _private4;
        private void* _private5;
        private void* _private6;

        #pragma warning restore CS0169
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe struct FLEncoder
    {
    }

    internal unsafe struct FLDictKey
    {
        #pragma warning disable CS0169

        // _private1[4] 
        private void* _private1a;
        private void* _private1b;
        private void* _private1c;
        private void* _private1d;
        private uint _private2;
        private uint _private3;
        private byte _private4;
        private byte _private5;

        #pragma warning restore CS0169
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe struct FLArray
    {
    }

    internal unsafe struct FLArrayIterator
    {
        #pragma warning disable CS0169

        private void* _private1;
        private uint _private2;
        private byte _private3;
        private void* _private4;

        #pragma warning restore CS0169
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe partial struct FLSliceResult
    {
        public void* buf;
        private UIntPtr _size;

        public ulong size
        {
            get {
                return _size.ToUInt64();
            }
            set {
                _size = (UIntPtr)value;
            }
        }
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe struct FLValue
    {
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe struct FLKeyPath
    {
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe struct FLSharedKeys
    {
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe partial struct FLSlice
    {
        public void* buf;
        private UIntPtr _size;

        public ulong size
        {
            get {
                return _size.ToUInt64();
            }
            set {
                _size = (UIntPtr)value;
            }
        }
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe struct FLDict
    {
    }
}
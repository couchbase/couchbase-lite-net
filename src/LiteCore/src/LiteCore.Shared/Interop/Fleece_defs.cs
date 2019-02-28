//
// Fleece_defs.cs
//
// Copyright (c) 2019 Couchbase, Inc All rights reserved.
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
    internal enum FLError
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
        POSIXError,
        Unsupported,
    }

    internal enum FLTrust
    {
        
        Untrusted,
        
        Trusted
    }

    internal enum FLValueType
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

    internal enum FLCopyFlags
    {
        DefaultCopy        = 0,
        DeepCopy           = 1,
        CopyImmutables     = 2,
        DeepCopyImmutables = (DeepCopy | CopyImmutables),
    }

    internal enum FLEncoderFormat
    {
        EncodeFleece,
        EncodeJSON,
        EncodeJSON5
    }

	internal unsafe struct FLValue
    {
    }

	internal unsafe struct FLArray
    {
    }

	internal unsafe struct FLDict
    {
    }

	internal unsafe struct FLMutableArray
    {
    }

	internal unsafe struct FLMutableDict
    {
    }

	internal unsafe struct FLDoc
    {
    }

	internal unsafe struct FLSharedKeys
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

    internal unsafe struct FLDictIterator
    {
        #pragma warning disable CS0169

        private void* _private1;
        private uint _private2;
        private byte _private3;

        // _private4[4]
        private void* _private4a;
        private void* _private4b;
        private void* _private4c;
        private void* _private4d;
        private int _private5;

        #pragma warning restore CS0169
    }

    internal unsafe struct FLDictKey
    {
        #pragma warning disable CS0169

        private FLSlice _private1;
        private void* _private2;
        private uint _private3;
        private uint _private4;
        private byte _private5;

        #pragma warning restore CS0169
    }

	internal unsafe struct FLDeepIterator
    {
    }

	internal unsafe struct FLPathComponent
    {
        public FLSlice key;
        public uint index;
    }

	internal unsafe struct FLKeyPath
    {
    }

	internal unsafe struct FLEncoder
    {
    }
}
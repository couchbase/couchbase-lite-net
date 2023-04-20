//
// FLCollections_native.cs
//
// Copyright (c) 2023 Couchbase, Inc All rights reserved.
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

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLArray_Count(FLArray* array);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLArray_IsEmpty(FLArray* array);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLArray_Get(FLArray* array, uint index);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLArrayIterator_Begin(FLArray* array, FLArrayIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLArrayIterator_GetValue(FLArrayIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLArrayIterator_GetValueAt(FLArrayIterator* i, uint offset);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLArrayIterator_GetCount(FLArrayIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLArrayIterator_Next(FLArrayIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint FLDict_Count(FLDict* dict);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLDict_IsEmpty(FLDict* dict);

        public static FLValue* FLDict_Get(FLDict* dict, byte[]? keyString)
        {
            fixed(byte *keyString_ = keyString) {
                return NativeRaw.FLDict_Get(dict, new FLSlice(keyString_, (ulong)keyString.Length));
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLDictIterator_Begin(FLDict* dict, FLDictIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDictIterator_GetKey(FLDictIterator* i);

        public static string? FLDictIterator_GetKeyString(FLDictIterator* i)
        {
            return NativeRaw.FLDictIterator_GetKeyString(i).CreateString();
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDictIterator_GetValue(FLDictIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLDictIterator_Next(FLDictIterator* i);

        // Note: Allocates unmanaged heap memory; should only be used with constants
        public static FLDictKey FLDictKey_Init(string str)
        {
            return NativeRaw.FLDictKey_Init(FLSlice.Constant(str));
        }

        public static string? FLDictKey_GetString(FLDictKey* dictKey)
        {
            return NativeRaw.FLDictKey_GetString(dictKey).CreateString();
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDict_GetWithKey(FLDict* dict, FLDictKey* dictKey);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLValue* FLDict_Get(FLDict* dict, FLSlice keyString);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLDictIterator_GetKeyString(FLDictIterator* i);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDictKey FLDictKey_Init(FLSlice @string);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLDictKey_GetString(FLDictKey* dictKey);


    }
}

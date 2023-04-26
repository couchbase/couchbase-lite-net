//
// FLMutable_native.cs
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
        public static extern void FLSlot_SetNull(FLSlot* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLSlot_SetBool(FLSlot* x, [MarshalAs(UnmanagedType.U1)]bool b);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLSlot_SetInt(FLSlot* x, long l);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLSlot_SetUInt(FLSlot* x, ulong u);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLSlot_SetFloat(FLSlot* x, float f);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLSlot_SetDouble(FLSlot* x, double d);

        public static void FLSlot_SetString(FLSlot* x, string? str)
        {
            using(var str_ = new C4String(str)) {
                NativeRaw.FLSlot_SetString(x, (FLSlice)str_.AsFLSlice());
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLSlot_SetValue(FLSlot* x, FLValue* value);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLSlot_SetString(FLSlot* x, FLSlice str);


    }
}

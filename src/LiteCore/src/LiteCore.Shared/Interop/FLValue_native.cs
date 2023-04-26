//
// FLValue_native.cs
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
        public static extern FLValueType FLValue_GetType(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_IsInteger(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_IsUnsigned(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_IsDouble(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool FLValue_AsBool(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long FLValue_AsInt(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong FLValue_AsUnsigned(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float FLValue_AsFloat(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern double FLValue_AsDouble(FLValue* value);

        public static string? FLValue_AsString(FLValue* value)
        {
            return NativeRaw.FLValue_AsString(value).CreateString();
        }

        public static byte[]? FLValue_AsData(FLValue* value)
        {
            return (NativeRaw.FLValue_AsData(value)).ToArrayFast();
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLArray* FLValue_AsArray(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLDict* FLValue_AsDict(FLValue* value);

        public static string? FLValue_ToString(FLValue* value)
        {
            using(var retVal = NativeRaw.FLValue_ToString(value)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FLValue_Release(FLValue* value);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLValue_AsString(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSlice FLValue_AsData(FLValue* value);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult FLValue_ToString(FLValue* value);


    }
}

//
// C4Error_native.cs
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
        public static string? c4error_getMessage(C4Error error)
        {
            using(var retVal = NativeRaw.c4error_getMessage(error)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        public static C4Error c4error_make(C4ErrorDomain domain, int code, string? message)
        {
            using(var message_ = new C4String(message)) {
                return NativeRaw.c4error_make(domain, code, (FLSlice)message_.AsFLSlice());
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4error_mayBeTransient(C4Error err);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4error_mayBeNetworkDependent(C4Error err);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4error_getMessage(C4Error error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Error c4error_make(C4ErrorDomain domain, int code, FLSlice message);


    }
}

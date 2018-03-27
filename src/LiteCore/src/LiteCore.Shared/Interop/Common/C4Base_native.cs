//
// C4Base_native.cs
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

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4SliceEqual(C4Slice a, C4Slice b);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4slice_free(C4SliceResult slice);

        public static string c4error_getMessage(C4Error error)
        {
            using(var retVal = NativeRaw.c4error_getMessage(error)) {
                return ((C4Slice)retVal).CreateString();
            }
        }

        public static C4Error c4error_make(C4ErrorDomain domain, int code, string message)
        {
            using(var message_ = new C4String(message)) {
                return NativeRaw.c4error_make(domain, code, message_.AsC4Slice());
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4error_mayBeTransient(C4Error err);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4error_mayBeNetworkDependent(C4Error err);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4log_writeToCallback(C4LogLevel level, C4LogCallback callback, [MarshalAs(UnmanagedType.U1)]bool preformatted);

        public static bool c4log_writeToBinaryFile(C4LogLevel level, string path, C4Error* error)
        {
            using(var path_ = new C4String(path)) {
                return NativeRaw.c4log_writeToBinaryFile(level, path_.AsC4Slice(), error);
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4LogLevel c4log_callbackLevel();

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4log_setCallbackLevel(C4LogLevel level);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4LogLevel c4log_binaryFileLevel();

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4log_setBinaryFileLevel(C4LogLevel level);

        public static string c4log_getDomainName(C4LogDomain* x)
        {
            var retVal = NativeRaw.c4log_getDomainName(x);
            return Marshal.PtrToStringAnsi((IntPtr)retVal);
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4LogLevel c4log_getLevel(C4LogDomain* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4log_setLevel(C4LogDomain* c4Domain, C4LogLevel level);

        public static void c4slog(C4LogDomain* domain, C4LogLevel level, string msg)
        {
            using(var msg_ = new C4String(msg)) {
                NativeRaw.c4slog(domain, level, msg_.AsC4Slice());
            }
        }

        public static string c4_getBuildInfo()
        {
            using(var retVal = NativeRaw.c4_getBuildInfo()) {
                return ((C4Slice)retVal).CreateString();
            }
        }

        public static string c4_getVersion()
        {
            using(var retVal = NativeRaw.c4_getVersion()) {
                return ((C4Slice)retVal).CreateString();
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int c4_getObjectCount();

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4_dumpInstances();


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4SliceResult c4error_getMessage(C4Error error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Error c4error_make(C4ErrorDomain domain, int code, C4Slice message);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4log_writeToBinaryFile(C4LogLevel level, C4Slice path, C4Error* error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte* c4log_getDomainName(C4LogDomain* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4slog(C4LogDomain* domain, C4LogLevel level, C4Slice msg);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4SliceResult c4_getBuildInfo();

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4SliceResult c4_getVersion();


    }
}

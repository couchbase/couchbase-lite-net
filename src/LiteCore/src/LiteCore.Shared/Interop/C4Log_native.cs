//
// C4Log_native.cs
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

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        public static string c4log_getDomainName(C4LogDomain* x)
        {
            var retVal = NativeRaw.c4log_getDomainName(x);
            return Marshal.PtrToStringAnsi((IntPtr)retVal);
        }

        public static void c4slog(C4LogDomain* domain, C4LogLevel level, string msg)
        {
            using (var msg_ = new C4String(msg))
            {
                NativeRaw.c4slog(domain, level, msg_.AsFLSlice());
            }
        }

    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4log_writeToBinaryFile(C4LogFileOptions options, C4Error* error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4LogLevel c4log_binaryFileLevel();

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4log_setBinaryFileLevel(C4LogLevel level);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4log_writeToCallback(C4LogLevel level, C4LogCallback callback, [MarshalAs(UnmanagedType.U1)] bool preformatted);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4LogLevel c4log_callbackLevel();

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4log_setCallbackLevel(C4LogLevel level);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4LogLevel c4log_getLevel(C4LogDomain* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4log_setLevel(C4LogDomain* c4Domain, C4LogLevel level);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4log_warnOnErrors([MarshalAs(UnmanagedType.U1)] bool b);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4log_enableFatalExceptionBacktrace();

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4slog(C4LogDomain* domain, C4LogLevel level, FLSlice msg);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte* c4log_getDomainName(C4LogDomain* x);

    }
}

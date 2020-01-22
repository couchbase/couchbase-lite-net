//
// C4Base_native.cs
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

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long c4_now();

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void* c4base_retain(void* obj);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4base_release(void* obj);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Database* c4db_retain(C4Database* @ref);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4db_release(C4Database* @ref);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Query* c4query_retain(C4Query* @ref);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4query_release(C4Query* @ref);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Document* c4doc_retain(C4Document* @ref);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4doc_release(C4Document* @ref);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4QueryEnumerator* c4queryenum_retain(C4QueryEnumerator* @ref);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4queryenum_release(C4QueryEnumerator* @ref);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4dbobs_free(C4DatabaseObserver* observer);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4docobs_free(C4DocumentObserver* observer);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4enum_free(C4DocEnumerator* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4raw_free(C4RawDocument* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4repl_free(C4Replicator* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4stream_close(C4ReadStream* stream);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4stream_closeWriter(C4WriteStream* stream);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int c4_getObjectCount();

        public static string c4error_getMessage(C4Error error)
        {
            using(var retVal = NativeRaw.c4error_getMessage(error)) {
                return ((FLSlice)retVal).CreateString();
            }
        }

        public static C4Error c4error_make(C4ErrorDomain domain, int code, string message)
        {
            using(var message_ = new C4String(message)) {
                return NativeRaw.c4error_make(domain, code, message_.AsFLSlice());
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

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool c4log_writeToBinaryFile(C4LogFileOptions options, C4Error* error);

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
                NativeRaw.c4slog(domain, level, msg_.AsFLSlice());
            }
        }

        public static string c4_getVersion()
        {
            using(var retVal = NativeRaw.c4_getVersion()) {
                return ((FLSlice)retVal).CreateString();
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4error_getMessage(C4Error error);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Error c4error_make(C4ErrorDomain domain, int code, FLSlice message);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte* c4log_getDomainName(C4LogDomain* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4slog(C4LogDomain* domain, C4LogLevel level, FLSlice msg);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern FLSliceResult c4_getVersion();


    }
}

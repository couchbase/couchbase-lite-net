//
// C4Socket_native.cs
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
        public static extern void c4socket_registerFactory(C4SocketFactory factory);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4Socket_setNativeHandle(C4Socket* socket, void* x);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void* c4Socket_getNativeHandle(C4Socket* socket);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_gotHTTPResponse(C4Socket* socket, int httpStatus, FLSlice responseHeadersFleece);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_opened(C4Socket* socket);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_closed(C4Socket* socket, C4Error errorIfAny);

        public static void c4socket_closeRequested(C4Socket* socket, int status, string? message)
        {
            using(var message_ = new C4String(message)) {
                NativeRaw.c4socket_closeRequested(socket, status, message_.AsFLSlice());
            }
        }

        public static void c4socket_completedWrite(C4Socket* socket, ulong byteCount)
        {
            NativeRaw.c4socket_completedWrite(socket, (UIntPtr)byteCount);
        }

        public static void c4socket_received(C4Socket* socket, byte[]? data)
        {
            fixed(byte *data_ = data) {
                NativeRaw.c4socket_received(socket, new FLSlice(data_, data == null ? 0 : (ulong)data.Length));
            }
        }

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern C4Socket* c4socket_fromNative(C4SocketFactory factory, void* nativeHandle, C4Address* address);


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_closeRequested(C4Socket* socket, int status, FLSlice message);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_completedWrite(C4Socket* socket, UIntPtr byteCount);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_received(C4Socket* socket, FLSlice data);


    }
}

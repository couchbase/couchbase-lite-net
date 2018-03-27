//
// C4Socket_native.cs
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
        public static extern void c4socket_registerFactory(C4SocketFactory factory);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_gotHTTPResponse(C4Socket* socket, int httpStatus, C4Slice responseHeadersFleece);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_opened(C4Socket* socket);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_closed(C4Socket* socket, C4Error errorIfAny);

        public static void c4socket_closeRequested(C4Socket* socket, int status, string message)
        {
            using(var message_ = new C4String(message)) {
                NativeRaw.c4socket_closeRequested(socket, status, message_.AsC4Slice());
            }
        }

        public static void c4socket_completedWrite(C4Socket* socket, ulong byteCount)
        {
            NativeRaw.c4socket_completedWrite(socket, (UIntPtr)byteCount);
        }

        public static void c4socket_received(C4Socket* socket, byte[] data)
        {
            fixed(byte *data_ = data) {
                NativeRaw.c4socket_received(socket, new C4Slice(data_, data == null ? 0 : (ulong)data.Length));
            }
        }


    }

    internal unsafe static partial class NativeRaw
    {
        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_closeRequested(C4Socket* socket, int status, C4Slice message);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_completedWrite(C4Socket* socket, UIntPtr byteCount);

        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void c4socket_received(C4Socket* socket, C4Slice data);


    }
}

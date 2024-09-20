//
// C4Socket_native_safe.cs
//
// Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

// Shadowing the C function naming style
#pragma warning disable IDE1006

using Couchbase.Lite.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LiteCore.Interop;

internal sealed unsafe class C4SocketWrapper : NativeWrapper
{
    public delegate void NativeCallback(C4Socket* s);
    public delegate T NativeCallback<T>(C4Socket* s);

    public C4Socket* RawSocket => (C4Socket*)_nativeInstance;

    public C4SocketWrapper(C4Socket* socket)
        : base((IntPtr)socket)
    {

    }

    public void UseSafe(NativeCallback a)
    {
        using var scope = BeginLockedScope(true);
        a(RawSocket);
    }

    public T UseSafe<T>(NativeCallback<T> a)
    {
        using var scope = BeginLockedScope(true);
        return a(RawSocket);
    }

    protected override void Dispose(bool disposing)
    {
        Native.c4socket_release(RawSocket);
    }
}

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods

    public static C4SocketWrapper? c4socket_fromNative(C4SocketFactory factory, void* nativeHandle, C4Address* address)
    {
        var rawSocket = Native.c4socket_fromNative(factory, nativeHandle, address);
        if(rawSocket == null) {
            return null;
        }

        // Noted in LiteCore headers, this return value must be immediately retained
        return new C4SocketWrapper(Native.c4socket_retain(rawSocket));
    }

    // Socket Exclusive Methods

    public static void c4socket_setNativeHandle(C4SocketWrapper socket, void* handle)
    {
        socket.UseSafe(s => Native.c4Socket_setNativeHandle(s, handle));
    }

    public static void* c4socket_getNativeHandle(C4SocketWrapper socket)
    {
        return (void *)socket.UseSafe(s => (IntPtr)Native.c4Socket_getNativeHandle(s));
    }

    public static void c4socket_gotHTTPResponse(C4SocketWrapper socket, int httpStatus, IDictionary<string, object>? headers)
    {
        socket.UseSafe(s => Native.c4socket_gotHTTPResponse(s, httpStatus, headers));
    }

    public static void c4socket_opened(C4SocketWrapper socket)
    {
        socket.UseSafe(Native.c4socket_opened);
    }

    public static void c4socket_closed(C4SocketWrapper socket, C4Error errorIfAny)
    {
        socket.UseSafe(s => Native.c4socket_closed(s, errorIfAny));
    }

    public static void c4socket_closeRequested(C4SocketWrapper socket, int status, string? message)
    {
        socket.UseSafe(s => Native.c4socket_closeRequested(s, status, message));
    }

    public static void c4socket_completedWrite(C4SocketWrapper socket, ulong byteCount)
    {
        socket.UseSafe(s => Native.c4socket_completedWrite(s, byteCount));
    }

    public static void c4socket_received(C4SocketWrapper socket, byte[]? data)
    {
        socket.UseSafe(s => Native.c4socket_received(s, data));
    }
}
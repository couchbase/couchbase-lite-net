//
// C4Socket.cs
//
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace LiteCore.Interop;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]   
internal unsafe delegate void SocketOpenDelegate(C4Socket* socket, C4Address* address, FLSlice options, void* context);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void SocketCloseDelegate(C4Socket* socket);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void SocketRequestCloseDelegate(C4Socket* socket, int status, FLSlice message);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void SocketWriteDelegate(C4Socket* socket, FLSliceResult allocatedData);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void SocketCompletedReceiveDelegate(C4Socket* socket, UIntPtr byteCount);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void SocketDisposeDelegate(C4Socket* socket);

internal unsafe delegate void SocketRequestCloseDelegateManaged(C4Socket* socket, int status, string message);

internal unsafe delegate void SocketWriteDelegateManaged(C4Socket* socket, byte[] allocatedData);

internal unsafe delegate void SocketCompletedReceiveDelegateManaged(C4Socket* socket, ulong byteCount);

internal unsafe delegate void SocketErrorDelegate(C4Socket* socket, Exception e);

[ExcludeFromCodeCoverage]
[SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
internal static unsafe class SocketFactory
{
    private static readonly SocketOpenDelegate Open;
    private static readonly SocketCloseDelegate Close;
    private static readonly SocketRequestCloseDelegate RequestClose;
    private static readonly SocketWriteDelegate Write;
    private static readonly SocketCompletedReceiveDelegate CompletedReceive;
    private static readonly SocketDisposeDelegate Dispose;

    private static SocketOpenDelegate? ExternalOpen;
    private static SocketCloseDelegate? ExternalClose;
    private static SocketRequestCloseDelegateManaged? ExternalRequestClose;
    private static SocketWriteDelegateManaged? ExternalWrite;
    private static SocketCompletedReceiveDelegateManaged? ExternalCompletedReceive;
    private static SocketErrorDelegate? Error;
    private static SocketDisposeDelegate? ExternalDispose;

    internal static C4SocketFactory InternalFactory { get; }

    static SocketFactory()
    {
        Open = SocketOpened;
        Close = SocketClose;
        RequestClose = SocketRequestClose;
        Write = SocketWrittenTo;
        CompletedReceive = SocketCompletedReceive;
        Dispose = SocketDispose;
        InternalFactory = new C4SocketFactory
        {
            framing = C4SocketFraming.WebSocketClientFraming,
            open = Marshal.GetFunctionPointerForDelegate(Open),
            close = Marshal.GetFunctionPointerForDelegate(Close),
            write = Marshal.GetFunctionPointerForDelegate(Write),
            completedReceive = Marshal.GetFunctionPointerForDelegate(CompletedReceive),
            dispose = Marshal.GetFunctionPointerForDelegate(Dispose)
        };

        Native.c4socket_registerFactory(InternalFactory);
    }

    #if __IOS__
    [ObjCRuntime.MonoPInvokeCallback(typeof(SocketRequestCloseDelegate))]
    #endif
    private static void SocketRequestClose(C4Socket* socket, int status, FLSlice message)
    {
        try {
            ExternalRequestClose?.Invoke(socket, status, message.CreateString() ?? "");
        } catch (Exception e) {
            Error?.Invoke(socket, new Exception("Error requesting socket close", e));
        }
    }

    #if __IOS__
    [ObjCRuntime.MonoPInvokeCallback(typeof(SocketDisposeDelegate))]
    #endif
    private static void SocketDispose(C4Socket* socket)
    {
        try {
            ExternalDispose?.Invoke(socket);
        } catch (Exception e) {
            Error?.Invoke(socket, new Exception("Error disposing socket", e));
        }
    }

    public static void RegisterFactory(SocketOpenDelegate doOpen, SocketCloseDelegate doClose, 
        SocketWriteDelegateManaged doWrite, SocketCompletedReceiveDelegateManaged doCompleteReceive,
        SocketDisposeDelegate doDispose)
    {
        ExternalOpen = doOpen;
        ExternalClose = doClose;
        ExternalWrite = doWrite;
        ExternalCompletedReceive = doCompleteReceive;
        ExternalDispose = doDispose;
    }

    public static void RegisterFactory(SocketOpenDelegate doOpen, SocketRequestCloseDelegateManaged doRequestClose, 
        SocketWriteDelegateManaged doWrite, SocketCompletedReceiveDelegateManaged doCompleteReceive,
        SocketDisposeDelegate doDispose)
    {
        ExternalOpen = doOpen;
        ExternalRequestClose = doRequestClose;
        ExternalWrite = doWrite;
        ExternalCompletedReceive = doCompleteReceive;
        ExternalDispose = doDispose;
    }

    public static void SetErrorHandler(SocketErrorDelegate doError)
    {
        Error = doError;
    }

    #if __IOS__
    [ObjCRuntime.MonoPInvokeCallback(typeof(SocketOpenDelegate))]
    #endif
    private static void SocketOpened(C4Socket* socket, C4Address* address, FLSlice options, void* context)
    {
        try {
            ExternalOpen?.Invoke(socket, address, options, context);
        } catch (Exception e) {
            Error?.Invoke(socket, new Exception("Error opening to socket", e));
            Native.c4socket_closed(socket, new C4Error(C4ErrorCode.UnexpectedError));
        }
    }

    #if __IOS__
    [ObjCRuntime.MonoPInvokeCallback(typeof(SocketCloseDelegate))]
    #endif
    private static void SocketClose(C4Socket* socket)
    {
        try {
			ExternalClose?.Invoke(socket);
        } catch (Exception e) {
            Error?.Invoke(socket, new Exception("Error closing socket", e));
        }
    }

    #if __IOS__
    [ObjCRuntime.MonoPInvokeCallback(typeof(SocketWriteDelegate))]
    #endif
    private static void SocketWrittenTo(C4Socket* socket, FLSliceResult allocatedData)
    {
        try {
            ExternalWrite?.Invoke(socket, ((FLSlice) allocatedData).ToArrayFast() ?? []);
        } catch (Exception e) {
            Error?.Invoke(socket, new Exception("Error writing to socket", e));
            Native.c4socket_closed(socket, new C4Error(C4ErrorCode.UnexpectedError));
        } finally {
            allocatedData.Dispose();
        }
    }

    #if __IOS__
    [ObjCRuntime.MonoPInvokeCallback(typeof(SocketCompletedReceiveDelegate))]
    #endif
    private static void SocketCompletedReceive(C4Socket* socket, UIntPtr byteCount)
    {
        try {
            ExternalCompletedReceive?.Invoke(socket, byteCount.ToUInt64());
        } catch (Exception e) {
            Error?.Invoke(socket, new Exception("Error completing receive for socket", e));
            Native.c4socket_closed(socket, new C4Error(C4ErrorCode.UnexpectedError));
        }
    }
}

internal static unsafe partial class Native
{
    public static void c4socket_gotHTTPResponse(C4Socket* socket, int httpStatus,
        IDictionary<string, object>? headers)
    {
        using var flHeaders = headers.FLEncode();
        c4socket_gotHTTPResponse(socket, httpStatus, (FLSlice)flHeaders);
    }
}
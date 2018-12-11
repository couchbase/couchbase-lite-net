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
using System.Runtime.InteropServices;

using ObjCRuntime;

namespace LiteCore.Interop
{
    using Couchbase.Lite.Interop;

    internal enum C4WebSocketCustomCloseCode
    {
        WebSocketCloseFirstAvailable = C4WebSocketCloseCode.WebSocketCloseFirstAvailable,
        WebSocketCloseUserTransient,
        WebSocketCloseUserPermanent
    }

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
    
    internal static unsafe class SocketFactory
    {
        private static readonly SocketOpenDelegate _open;
        private static readonly SocketCloseDelegate _close;
        private static readonly SocketRequestCloseDelegate _requestClose;
        private static readonly SocketWriteDelegate _write;
        private static readonly SocketCompletedReceiveDelegate _completedReceive;
        private static readonly SocketDisposeDelegate _dispose;

        private static SocketOpenDelegate _externalOpen;
        private static SocketCloseDelegate _externalClose;
        private static SocketRequestCloseDelegateManaged _externalRequestClose;
        private static SocketWriteDelegateManaged _externalWrite;
        private static SocketCompletedReceiveDelegateManaged _externalCompletedReceive;
        private static SocketErrorDelegate _error;
        private static SocketDisposeDelegate _externalDispose;

        internal static C4SocketFactory InternalFactory { get; }

        static SocketFactory()
        {
            _open = SocketOpened;
            _close = SocketClose;
            _requestClose = SocketRequestClose;
            _write = SocketWrittenTo;
            _completedReceive = SocketCompletedReceive;
            _dispose = SocketDispose;
            InternalFactory = new C4SocketFactory
            {
                framing = C4SocketFraming.WebSocketClientFraming,
                open = Marshal.GetFunctionPointerForDelegate(_open),
                close = Marshal.GetFunctionPointerForDelegate(_close),
                write = Marshal.GetFunctionPointerForDelegate(_write),
                completedReceive = Marshal.GetFunctionPointerForDelegate(_completedReceive),
                dispose = Marshal.GetFunctionPointerForDelegate(_dispose)
            };

            Native.c4socket_registerFactory(InternalFactory);
        }

        [MonoPInvokeCallback(typeof(SocketRequestCloseDelegate))]
        private static void SocketRequestClose(C4Socket* socket, int status, FLSlice message)
        {
            try {
                _externalRequestClose?.Invoke(socket, status, message.CreateString());
            } catch (Exception e) {
                _error?.Invoke(socket, new Exception("Error requesting socket close", e));
            }
        }

        [MonoPInvokeCallback(typeof(SocketDisposeDelegate))]
        private static void SocketDispose(C4Socket* socket)
        {
            try {
                _externalDispose?.Invoke(socket);
            } catch (Exception e) {
                _error?.Invoke(socket, new Exception("Error disposing socket", e));
            }
        }

        public static void RegisterFactory(SocketOpenDelegate doOpen, SocketCloseDelegate doClose, 
            SocketWriteDelegateManaged doWrite, SocketCompletedReceiveDelegateManaged doCompleteReceive,
            SocketDisposeDelegate doDispose)
        {
            _externalOpen = doOpen;
            _externalClose = doClose;
            _externalWrite = doWrite;
            _externalCompletedReceive = doCompleteReceive;
            _externalDispose = doDispose;
        }

        public static void RegisterFactory(SocketOpenDelegate doOpen, SocketRequestCloseDelegateManaged doRequestClose, 
            SocketWriteDelegateManaged doWrite, SocketCompletedReceiveDelegateManaged doCompleteReceive,
            SocketDisposeDelegate doDispose)
        {
            _externalOpen = doOpen;
            _externalRequestClose = doRequestClose;
            _externalWrite = doWrite;
            _externalCompletedReceive = doCompleteReceive;
            _externalDispose = doDispose;
        }

        public static void SetErrorHandler(SocketErrorDelegate doError)
        {
            _error = doError;
        }

        [MonoPInvokeCallback(typeof(SocketOpenDelegate))]
        private static void SocketOpened(C4Socket* socket, C4Address* address, FLSlice options, void* context)
        {
            try {
                _externalOpen?.Invoke(socket, address, options, context);
            } catch (Exception e) {
                _error?.Invoke(socket, new Exception("Error opening to socket", e));
                Native.c4socket_closed(socket, new C4Error(C4ErrorCode.UnexpectedError));
            }
        }

        [MonoPInvokeCallback(typeof(SocketCloseDelegate))]
        private static void SocketClose(C4Socket* socket)
        {
            try {
				_externalClose?.Invoke(socket);
            } catch (Exception e) {
                _error?.Invoke(socket, new Exception("Error closing socket", e));
            }
        }

        [MonoPInvokeCallback(typeof(SocketWriteDelegate))]
        private static void SocketWrittenTo(C4Socket* socket, FLSliceResult allocatedData)
        {
            try {
                _externalWrite?.Invoke(socket, ((FLSlice) allocatedData).ToArrayFast());
            } catch (Exception e) {
                _error?.Invoke(socket, new Exception("Error writing to socket", e));
                Native.c4socket_closed(socket, new C4Error(C4ErrorCode.UnexpectedError));
            } finally {
                allocatedData.Dispose();
            }
        }

        [MonoPInvokeCallback(typeof(SocketCompletedReceiveDelegate))]
        private static void SocketCompletedReceive(C4Socket* socket, UIntPtr byteCount)
        {
            try {
                _externalCompletedReceive?.Invoke(socket, byteCount.ToUInt64());
            } catch (Exception e) {
                _error?.Invoke(socket, new Exception("Error completing receive for socket", e));
                Native.c4socket_closed(socket, new C4Error(C4ErrorCode.UnexpectedError));
            }
        }
    }
}

namespace Couchbase.Lite.Interop
{
    using LiteCore.Interop;

    internal static unsafe partial class Native
    {
        public static void c4socket_gotHTTPResponse(C4Socket* socket, int httpStatus,
            IDictionary<string, object> headers)
        {
            using (var headers_ = headers.FLEncode()) {
                c4socket_gotHTTPResponse(socket, httpStatus, (FLSlice)headers_);
            }
        }
    }
}
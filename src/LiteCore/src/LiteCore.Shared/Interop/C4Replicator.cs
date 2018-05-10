//
// C4Replicator.cs
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
using System.Threading;

using LiteCore.Interop;
using LiteCore.Util;
using ObjCRuntime;

namespace LiteCore.Interop
{

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4ReplicatorStatusChangedCallback(C4Replicator* replicator,
            C4ReplicatorStatus replicatorState, void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal unsafe delegate bool C4ReplicatorValidationFunction(C4Slice docID,
            FLDict* body, void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4ReplicatorDocumentErrorCallback(C4Replicator* replicator,
            [MarshalAs(UnmanagedType.U1)]bool pushing, C4Slice docID, C4Error error, 
            [MarshalAs(UnmanagedType.U1)]bool transient, void* context);
}

namespace Couchbase.Lite.Interop
{
    internal sealed class ReplicatorParameters : IDisposable
    {
        private C4ReplicatorParameters _c4Params;
        private C4ReplicatorStatusChangedCallback _onStatusChanged;
        private C4ReplicatorDocumentErrorCallback _onDocumentError;

        public C4ReplicatorParameters C4Params => _c4Params;

        public ReplicatorParameters(IDictionary<string, object> options)
        {
            if (options != null) {
                _c4Params.optionsDictFleece = (C4Slice) options.FLEncode();
            }
        }

        public C4ReplicatorMode Push
        {
            get => _c4Params.push;
            set => _c4Params.push = value;
        }

        public C4ReplicatorMode Pull
        {
            get => _c4Params.pull;
            set => _c4Params.pull = value;
        }

        public C4ReplicatorStatusChangedCallback OnStatusChanged
        {
            get => _onStatusChanged;
            set {
                _onStatusChanged = value;
                _c4Params.onStatusChanged = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        public C4ReplicatorDocumentErrorCallback OnDocumentError
        {
            get => _onDocumentError;
            set {
                _onDocumentError = value;
                _c4Params.onDocumentError = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        public unsafe object Context
        {
            get => GCHandle.FromIntPtr((IntPtr) _c4Params.callbackContext).Target;
            set {
                if (_c4Params.callbackContext != null) {
                    GCHandle.FromIntPtr((IntPtr)_c4Params.callbackContext).Free();
                }

                if (value != null) {
                    _c4Params.callbackContext = GCHandle.ToIntPtr(GCHandle.Alloc(value)).ToPointer();
                }
            }
        }

        public unsafe C4SocketFactory* SocketFactory
        {
            get => _c4Params.socketFactory;
            set => _c4Params.socketFactory = value;
        }

        public void Dispose()
        {
            Native.FLSliceResult_Free((FLSliceResult)_c4Params.optionsDictFleece);
            Context = null;
        }
    }

    internal static unsafe partial class Native
    {
        public static C4Replicator* c4repl_new(C4Database* db, C4Address remoteAddress, string remoteDatabaseName,
            C4Database *otherDb, C4ReplicatorParameters @params, C4Error* err)
        {
            using (var remoteDatabaseName_ = new C4String(remoteDatabaseName)) {
                return c4repl_new(db, remoteAddress, remoteDatabaseName_.AsC4Slice(), otherDb, @params, err);
            }
        }

        public static IDictionary<string, object> bridge_c4repl_getResponseHeaders(C4Replicator* repl)
        {
            var result = c4repl_getResponseHeaders(repl);
            return FLSliceExtensions.ToObject(NativeRaw.FLValue_FromTrustedData((FLSlice) result)) as
                IDictionary<string, object>;
        }
    }
}

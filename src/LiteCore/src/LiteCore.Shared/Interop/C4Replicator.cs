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
#if LITECORE_PACKAGED
    internal
#else
    public
#endif
        unsafe delegate void C4ReplicatorStatusChangedCallback(C4Replicator* replicator,
            C4ReplicatorStatus replicatorState, void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
#if LITECORE_PACKAGED
    internal
#else
    public
#endif
        unsafe delegate bool C4ReplicatorValidationFunction(C4Slice docID,
            FLDict* body, void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#if LITECORE_PACKAGED
    internal
#else
    public
#endif
        unsafe delegate void C4ReplicatorDocumentErrorCallback(C4Replicator* replicator,
            [MarshalAs(UnmanagedType.U1)]bool pushing, C4Slice docID, C4Error error, 
            [MarshalAs(UnmanagedType.U1)]bool transient, void* context);


#if LITECORE_PACKAGED
    internal
#else
    public
#endif
        unsafe partial struct C4ReplicatorParameters
    {
        public C4ReplicatorParameters(C4ReplicatorMode push, C4ReplicatorMode pull, C4Slice optionsFleece, void* context)
        {
            this.push = push;
            this.pull = pull;
            optionsDictFleece = optionsFleece;
            validationFunc = Marshal.GetFunctionPointerForDelegate(ReplicatorParameters.NativeValidateFunction);
            onStatusChanged = Marshal.GetFunctionPointerForDelegate(ReplicatorParameters.NativeChangedCallback);
            onDocumentError = Marshal.GetFunctionPointerForDelegate(ReplicatorParameters.NativeErrorCallback);
            callbackContext = context;
        }
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
        unsafe sealed class ReplicatorParameters : IDisposable
    {
        private readonly object _context;
        private void* _nativeContext;
        private readonly Action<C4ReplicatorStatus, object> _stateChangedCallback;
        private readonly Action<bool, string, C4Error, bool, object> _errorCallback;
        private readonly Func<string, IntPtr, object, bool> _validateFunction;
        private readonly long _id;
        private static long _NextID;

        private static readonly Dictionary<long, ReplicatorParameters> _StaticMap =
            new Dictionary<long, ReplicatorParameters>();

        internal static C4ReplicatorDocumentErrorCallback NativeErrorCallback { get; }

        internal static C4ReplicatorValidationFunction NativeValidateFunction { get; }

        internal static C4ReplicatorStatusChangedCallback NativeChangedCallback { get; }

        internal C4ReplicatorParameters C4Params { get; }

        internal C4SocketFactory SocketFactory { get; set; }

        static ReplicatorParameters()
        {
            NativeErrorCallback = OnError;
            NativeValidateFunction = PerformValidate;
            NativeChangedCallback = StateChanged;
        }

        public ReplicatorParameters(C4ReplicatorMode push, C4ReplicatorMode pull, IDictionary<string, object> options,
            Func<string, IntPtr, object, bool> validateFunction, Action<bool, string, C4Error, bool, object> errorCallback,
            Action<C4ReplicatorStatus, object> stateChangedCallback, object context)
        {
            var nextId = Interlocked.Increment(ref _NextID);
            _id = nextId;
            _validateFunction = validateFunction;
            _errorCallback = errorCallback;
            _stateChangedCallback = stateChangedCallback;
            _context = context;
            _nativeContext = (void*)nextId;
            C4Params = new C4ReplicatorParameters(push, pull, (C4Slice)options.FLEncode(), _nativeContext);
            _StaticMap[_id] = this;
        }

        [MonoPInvokeCallback(typeof(C4ReplicatorDocumentErrorCallback))]
        private static void OnError(C4Replicator* replicator, bool pushing, C4Slice docID, C4Error error,
            bool transient, void* context)
        {
            // Don't throw exceptions here, it will bubble up to native code
            try {
                var id = (long)context;
                var obj = _StaticMap[id];
                obj._errorCallback?.Invoke(pushing, docID.CreateString(), error, transient, obj._context);
            } catch (Exception) { }
        }

        [MonoPInvokeCallback(typeof(C4ReplicatorValidationFunction))]
        private static bool PerformValidate(C4Slice docID, FLDict* body, void* context)
        {
            // Don't throw exceptions here, it will bubble up to native code
            try {
                var id = (long) context;
                var obj = _StaticMap[id];
                var result = obj._validateFunction?.Invoke(docID.CreateString(), (IntPtr) body, obj._context);
                return result.HasValue && result.Value;
            } catch (Exception) {
                return false;
            }
        }

        [MonoPInvokeCallback(typeof(C4ReplicatorStatusChangedCallback))]
        private static void StateChanged(C4Replicator* replicator, C4ReplicatorStatus state, void* context)
        {
            // Don't throw exceptions here, it will bubble up to native code
            try {
                var id = (long) context;
                var obj = _StaticMap[id];
                obj._stateChangedCallback?.Invoke(state, obj._context);
            } catch(Exception) { }
        }

        public void Dispose()
        {
            _StaticMap.Remove(_id);
            var flSliceResult = new FLSliceResult
            {
                buf = C4Params.optionsDictFleece.buf,
                size = C4Params.optionsDictFleece.size
            };
            flSliceResult.Dispose();
        }
    }
}

namespace Couchbase.Lite.Interop
{
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

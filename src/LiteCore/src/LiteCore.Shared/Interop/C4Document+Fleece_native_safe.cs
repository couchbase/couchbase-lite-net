//
// C4Document+Fleece_native_safe.cs
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

using Couchbase.Lite;
using Couchbase.Lite.Support;

using System;
using System.Runtime.InteropServices;

namespace LiteCore.Interop;

// This class is meant to be used all in one scope.  It will hold
// the thread safety lock until disposed!  It also doesn't dispose
// the native object because it is meant to be used on the db shared
// FLEncoder, which is not meant to be freed.
internal sealed unsafe class FLEncoderWrapper(FLEncoder* native, ThreadSafety threadSafety) : IDisposable
{
    private readonly IDisposable _scopeExit = threadSafety.BeginLockedScope();
    private GCHandle _contextHandle;

    public void BeginDict(ulong capacity) => Native.FLEncoder_BeginDict(native, capacity);

    public void EndDict() => Native.FLEncoder_EndDict(native);

    public void SetExtraInfo(object extraInfo)
    {
        _contextHandle = GCHandle.Alloc(extraInfo);
        Native.FLEncoder_SetExtraInfo(native, (void*)GCHandle.ToIntPtr(_contextHandle));
    }

    public void Encode<T>(T obj) => obj.FLEncode(native);

    public FLSliceResult Finish()
    {
        FLError error;
        var body = NativeRaw.FLEncoder_Finish(native, &error);
        return body.buf == null ? throw new CouchbaseFleeceException(error) : body;
    }

    public void Reset() => Native.FLEncoder_Reset(native);

    public void Dispose()
    {
        if (_contextHandle.IsAllocated) {
            _contextHandle.Free();
        }

        _scopeExit.Dispose();
    }
}

internal static unsafe partial class NativeSafe
{
    // Database Exclusive Methods

    public static FLDict* c4doc_getProperties(C4DocumentWrapper doc) =>
        (FLDict *)doc.UseSafe(d => (IntPtr)Native.c4doc_getProperties(d), 
            C4DocumentWrapper.ThreadSafetyLevel.Database);

    public static bool c4doc_dictContainsBlobs(C4DatabaseWrapper database, FLDict* dict) => 
        database.UseSafe(_ => Native.c4doc_dictContainsBlobs(dict));

    public static string? c4doc_bodyAsJSON(C4DocumentWrapper doc, bool canonical, C4Error* outError) =>
        doc.UseSafe(d => Native.c4doc_bodyAsJSON(d, canonical, outError),
            C4DocumentWrapper.ThreadSafetyLevel.Database);


    public static FLEncoderWrapper c4db_getSharedFleeceEncoder(C4DatabaseWrapper database)
    {
        var encoder = (FLEncoder*)database.UseSafe(db => (IntPtr)Native.c4db_getSharedFleeceEncoder(db));
        return new FLEncoderWrapper(encoder, database.InstanceSafety);
    }

    public static FLSharedKeys* c4db_getFLSharedKeys(C4DatabaseWrapper database) => 
        (FLSharedKeys*)database.UseSafe(db => (IntPtr)Native.c4db_getFLSharedKeys(db));
}
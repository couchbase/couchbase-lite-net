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
using Couchbase.Lite.Util;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace LiteCore.Interop;

// This class is meant to used all in one scope.  It will hold
// the thread safety lock until disposed!  It also doesn't dispose
// the native object because it is meant to be used on the db shared
// FLEncoder, which is not meant to be freed.
internal unsafe sealed class FLEncoderWrapper : IDisposable
{
    private readonly IDisposable _scopeExit;
    private readonly ThreadSafety _threadSafety;
    private readonly FLEncoder* _encoder;
    private GCHandle _contextHandle;

    public FLEncoderWrapper(FLEncoder* native, ThreadSafety threadSafety)
    {
        _threadSafety = threadSafety;
        _scopeExit = _threadSafety.BeginLockedScope();
        _encoder = native;
    }

    public void BeginDict(ulong capacity)
    {
        Native.FLEncoder_BeginDict(_encoder, capacity);
    }

    public void EndDict()
    {
        Native.FLEncoder_EndDict(_encoder);
    }

    public void SetExtraInfo(object extraInfo)
    {
        _contextHandle = GCHandle.Alloc(extraInfo);
        Native.FLEncoder_SetExtraInfo(_encoder, (void*)GCHandle.ToIntPtr(_contextHandle));
    }

    public void Encode<T>(T obj)
    {
        obj.FLEncode(_encoder);
    }

    public FLSliceResult Finish()
    {
        FLError error;
        var body = NativeRaw.FLEncoder_Finish(_encoder, &error);
        if (body.buf == null) {
            throw new CouchbaseFleeceException(error);
        }

        return body;
    }

    public void Reset()
    {
        Native.FLEncoder_Reset(_encoder);
    }

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

    public static FLDict* c4doc_getProperties(C4DocumentWrapper doc)
    {
        return (FLDict *)doc.UseSafe(d => (IntPtr)Native.c4doc_getProperties(d), 
            C4DocumentWrapper.ThreadSafetyLevel.Database);
    }

    public static bool c4doc_dictContainsBlobs(C4DatabaseWrapper database, FLDict* dict)
    {
        return database.UseSafe(db => Native.c4doc_dictContainsBlobs(dict));
    }

    public static string? c4doc_bodyAsJSON(C4DocumentWrapper doc, bool canonical, C4Error* outError)
    {
        return doc.UseSafe(d => Native.c4doc_bodyAsJSON(d, canonical, outError),
            C4DocumentWrapper.ThreadSafetyLevel.Database);
    }


    public static FLEncoderWrapper c4db_getSharedFleeceEncoder(C4DatabaseWrapper database)
    {
        var encoder = (FLEncoder*)database.UseSafe(db => (IntPtr)Native.c4db_getSharedFleeceEncoder(db));

        return new FLEncoderWrapper(encoder, database.InstanceSafety);
    }

    public static FLSharedKeys* c4db_getFLSharedKeys(C4DatabaseWrapper database)
    {
        return (FLSharedKeys*)database.UseSafe(db => (IntPtr)Native.c4db_getFLSharedKeys(db));
    }
}
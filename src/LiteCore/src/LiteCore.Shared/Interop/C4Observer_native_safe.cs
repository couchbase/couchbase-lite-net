//
// C4Observer_native_safe.cs
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
using System.Runtime.CompilerServices;

namespace LiteCore.Interop;

// This wrapper shares a lock with its owning database
internal sealed unsafe class C4QueryObserverWrapper(C4QueryObserver* obs, ThreadSafety threadSafety)
    : NativeWrapper((IntPtr)obs, threadSafety)
{
    public delegate void NativeCallback(C4QueryObserver* o);
    public delegate T NativeCallback<out T>(C4QueryObserver* o);

    public C4QueryObserver* RawObserver => (C4QueryObserver*)_nativeInstance;
    
    public void UseSafe(NativeCallback a)
    {
        using var scope = BeginLockedScope(true);
        a(RawObserver);
    }

    protected override void Dispose(bool disposing) => Native.c4queryobs_free(RawObserver);
}

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static C4CollectionObservation c4dbobs_getChanges(C4CollectionObserver* obs, C4CollectionChange[] outChanges, uint maxChanges) => 
        Native.c4dbobs_getChanges(obs, outChanges, maxChanges);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void c4dbobs_releaseChanges(C4CollectionChange[] outChanges, uint numChanges) => 
        Native.c4dbobs_releaseChanges(outChanges, numChanges);

    public static C4DocumentObserver* c4docobs_createWithCollection(C4CollectionWrapper collection, string docID, 
        C4DocumentObserverCallback callback, void* context, C4Error* outError) => 
        Native.c4docobs_createWithCollection(collection.RawCollection, docID, callback, context, outError);

    public static C4QueryObserverWrapper c4queryobs_create(C4QueryWrapper query, C4QueryObserverCallback callback, void* context)
    {
        // This method is thread-safe, but we need the wrapper for a future non threadsafe method (c4queryobs_setEnabled)
        var rawObs = Native.c4queryobs_create(query.RawQuery, callback, context);
        return new C4QueryObserverWrapper(rawObs, query.DatabaseThreadSafety);
    }

    public static C4QueryEnumeratorWrapper? c4queryobs_getEnumerator(C4QueryObserverWrapper obs, bool forget, C4Error* outError)
    {
        var rawEnum = Native.c4queryobs_getEnumerator(obs.RawObserver, forget, outError);
        return rawEnum == null ? null : new C4QueryEnumeratorWrapper(rawEnum, obs.InstanceSafety);
    }

    // Database Exclusive Methods

    public static C4CollectionObserver* c4dbobs_createOnCollection(C4CollectionWrapper collection, C4CollectionObserverCallback callback, void* context, C4Error* outError) => 
        (C4CollectionObserver*)collection.UseSafe(c => (IntPtr)Native.c4dbobs_createOnCollection(c, callback, context, outError));

    public static void c4queryobs_setEnabled(C4QueryObserverWrapper obs, bool enabled)
    {
        // Noted exception in thread safety documents.  Only setting to true
        // needs to be protected
        if (enabled) {
            obs.UseSafe(o => Native.c4queryobs_setEnabled(o, true));
        } else {
            Native.c4queryobs_setEnabled(obs.RawObserver, false);
        }
    }
}
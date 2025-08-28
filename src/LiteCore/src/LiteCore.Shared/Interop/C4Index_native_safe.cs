//
// C4Index_native_safe.cs
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

using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Support;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LiteCore.Interop;

internal sealed unsafe class C4IndexWrapper : NativeWrapper
{
    public delegate T NativeWrapper<T>(C4Index* i);

    public C4Index* RawIndex => (C4Index*)_nativeInstance;

    // This wrapper shares a lock with its owning database
    public C4IndexWrapper(C4Index* index, ThreadSafety threadSafety)
        : base((IntPtr)index, threadSafety)
    {

    }

    public T UseSafe<T>(NativeWrapper<T> a)
    {
        using var scope = BeginLockedScope(true);
        return a(RawIndex);
    }

    protected override void Dispose(bool disposing)
    {
        Native.c4index_release(RawIndex);
    }
}

internal sealed unsafe class C4IndexUpdaterWrapper : NativeWrapper
{
    public delegate T NativeWrapper<T>(C4IndexUpdater* i);

    [Flags]
    public enum ThreadSafetyLevel
    {
        Updater = 1 << 0,
        Database = 1 << 1
    }

    public ThreadSafety DatabaseThreadSafety { get; }

    public C4IndexUpdater* RawUpdater => (C4IndexUpdater*)_nativeInstance;

    public C4IndexUpdaterWrapper(C4IndexUpdater* index, ThreadSafety databaseThreadSafety)
        : base((IntPtr)index)
    {
        DatabaseThreadSafety = databaseThreadSafety;
    }

    public T UseSafe<T>(NativeWrapper<T> a, ThreadSafetyLevel safetyLevel)
    {
        var withInstance = safetyLevel.HasFlag(ThreadSafetyLevel.Updater);
        var additional = safetyLevel.HasFlag(ThreadSafetyLevel.Database) ?
            Enumerable.Repeat(DatabaseThreadSafety, 1) : Enumerable.Empty<ThreadSafety>();

        using var scope = BeginLockedScope(withInstance, additional.ToArray());

        return a(RawUpdater);
    }

    protected override void Dispose(bool disposing)
    {
        Native.c4indexupdater_release(RawUpdater);
    }
}

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods

    public static ulong c4indexupdater_count(C4IndexUpdaterWrapper updater)
    {
        return Native.c4indexupdater_count(updater.RawUpdater).ToUInt64();
    }

    // Database Exclusive Methods

    public static bool c4coll_createIndex(C4CollectionWrapper collection, string name, 
        string? indexSpec, C4QueryLanguage queryLanguage, C4IndexType indexType, 
        C4IndexOptions* indexOptions, C4Error* outError)
    {
        return collection.UseSafe(c =>
            Native.c4coll_createIndex(c, name, indexSpec, queryLanguage, indexType, indexOptions, outError));
    }

    public static C4IndexWrapper? c4coll_getIndex(C4CollectionWrapper collection, string name, C4Error* outError)
    {
        var rawIndex = (C4Index *)collection.UseSafe(c =>
            (IntPtr)Native.c4coll_getIndex(c, name, outError));
        if(rawIndex == null) {
            return null;
        }

        return new C4IndexWrapper(rawIndex, collection.InstanceSafety);
    }

    public static bool c4coll_deleteIndex(C4CollectionWrapper collection, string name, C4Error* outError)
    {
        return collection.UseSafe(c => Native.c4coll_deleteIndex(c, name, outError));
    }

    public static FLSliceResult c4coll_getIndexesInfo(C4CollectionWrapper collection, C4Error* outError)
    {
        return collection.UseSafe(c => NativeRaw.c4coll_getIndexesInfo(c, outError));
    }

    public static bool c4index_isTrained(C4IndexWrapper index, C4Error* outError)
    {
        return index.UseSafe(i => Native.c4index_isTrained(i, outError));
    }

    public static C4IndexUpdaterWrapper? c4index_beginUpdate(C4IndexWrapper index, ulong limit, C4Error* outError)
    {
        var rawUpdater = (C4IndexUpdater*)index.UseSafe(i => (IntPtr)Native.c4index_beginUpdate(i, limit, outError));
        if(rawUpdater == null) {
            return null;
        }

        return new C4IndexUpdaterWrapper(rawUpdater, index.InstanceSafety);
    }

    public static bool c4indexupdater_finish(C4IndexUpdaterWrapper updater, C4Error* outError)
    {
        return updater.UseSafe(u => Native.c4indexupdater_finish(u, outError),
            C4IndexUpdaterWrapper.ThreadSafetyLevel.Database);
    }

    // Index Updater Exclusive Methods

    public static FLValue* c4indexupdater_valueAt(C4IndexUpdaterWrapper updater, ulong i)
    {
        return (FLValue *)updater.UseSafe(u => (IntPtr)Native.c4indexupdater_valueAt(u, i),
            C4IndexUpdaterWrapper.ThreadSafetyLevel.Updater);
    }

    public static bool c4indexupdater_setVectorAt(C4IndexUpdaterWrapper updater, ulong i, float* vector, 
        ulong dimension, C4Error* outError)
    {
        return updater.UseSafe(u => Native.c4indexupdater_setVectorAt(u, i, vector, dimension, outError),
            C4IndexUpdaterWrapper.ThreadSafetyLevel.Updater);
    }

    public static bool c4indexupdater_skipVectorAt(C4IndexUpdaterWrapper updater, ulong i)
    {
        return updater.UseSafe(u => Native.c4indexupdater_skipVectorAt(u, i),
            C4IndexUpdaterWrapper.ThreadSafetyLevel.Updater);
    }
}
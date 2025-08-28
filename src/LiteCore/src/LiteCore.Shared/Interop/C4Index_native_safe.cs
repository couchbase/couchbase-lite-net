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

using Couchbase.Lite.Support;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace LiteCore.Interop;

// This wrapper shares a lock with its owning database
internal sealed unsafe class C4IndexWrapper(C4Index* index, ThreadSafety threadSafety) : NativeWrapper((IntPtr)index, threadSafety)
{
    public delegate T NativeWrapper<out T>(C4Index* i);

    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public C4Index* RawIndex => (C4Index*)_nativeInstance;


    public T UseSafe<T>(NativeWrapper<T> a)
    {
        using var scope = BeginLockedScope(true);
        return a(RawIndex);
    }

    protected override void Dispose(bool disposing) => Native.c4index_release(RawIndex);
}

internal sealed unsafe class C4IndexUpdaterWrapper(C4IndexUpdater* index, ThreadSafety databaseThreadSafety) : NativeWrapper((IntPtr)index)
{
    public delegate T NativeWrapper<out T>(C4IndexUpdater* i);

    [Flags]
    public enum ThreadSafetyLevel
    {
        Updater = 1 << 0,
        Database = 1 << 1
    }

    public ThreadSafety DatabaseThreadSafety { get; } = databaseThreadSafety;

    public C4IndexUpdater* RawUpdater => (C4IndexUpdater*)_nativeInstance;

    public T UseSafe<T>(NativeWrapper<T> a, ThreadSafetyLevel safetyLevel)
    {
        var withInstance = safetyLevel.HasFlag(ThreadSafetyLevel.Updater);
        var additional = safetyLevel.HasFlag(ThreadSafetyLevel.Database) ?
            Enumerable.Repeat(DatabaseThreadSafety, 1) : [];

        using var scope = BeginLockedScope(withInstance, additional.ToArray());

        return a(RawUpdater);
    }

    protected override void Dispose(bool disposing) => Native.c4indexupdater_release(RawUpdater);
}

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods

    public static ulong c4indexupdater_count(C4IndexUpdaterWrapper updater) => Native.c4indexupdater_count(updater.RawUpdater).ToUInt64();

    // Database Exclusive Methods

    public static bool c4coll_createIndex(C4CollectionWrapper collection, string name, 
        string? indexSpec, C4QueryLanguage queryLanguage, C4IndexType indexType, 
        C4IndexOptions* indexOptions, C4Error* outError) =>
        collection.UseSafe(c =>
            Native.c4coll_createIndex(c, name, indexSpec, queryLanguage, indexType, indexOptions, outError));

    public static C4IndexWrapper? c4coll_getIndex(C4CollectionWrapper collection, string name, C4Error* outError)
    {
        var rawIndex = (C4Index *)collection.UseSafe(c =>
            (IntPtr)Native.c4coll_getIndex(c, name, outError));
        return rawIndex == null ? null : new C4IndexWrapper(rawIndex, collection.InstanceSafety);
    }

    public static bool c4coll_deleteIndex(C4CollectionWrapper collection, string name, C4Error* outError) => 
        collection.UseSafe(c => Native.c4coll_deleteIndex(c, name, outError));

    public static FLSliceResult c4coll_getIndexesInfo(C4CollectionWrapper collection, C4Error* outError) => 
        collection.UseSafe(c => Native.c4coll_getIndexesInfo(c, outError));

    public static bool c4index_isTrained(C4IndexWrapper index, C4Error* outError) => 
        index.UseSafe(i => Native.c4index_isTrained(i, outError));

    public static C4IndexUpdaterWrapper? c4index_beginUpdate(C4IndexWrapper index, ulong limit, C4Error* outError)
    {
        var rawUpdater = (C4IndexUpdater*)index.UseSafe(i => (IntPtr)Native.c4index_beginUpdate(i, limit, outError));
        return rawUpdater == null ? null : new C4IndexUpdaterWrapper(rawUpdater, index.InstanceSafety);
    }

    public static bool c4indexupdater_finish(C4IndexUpdaterWrapper updater, C4Error* outError) =>
        updater.UseSafe(u => Native.c4indexupdater_finish(u, outError),
            C4IndexUpdaterWrapper.ThreadSafetyLevel.Database);

    // Index Updater Exclusive Methods

    public static FLValue* c4indexupdater_valueAt(C4IndexUpdaterWrapper updater, ulong i) =>
        (FLValue *)updater.UseSafe(u => (IntPtr)Native.c4indexupdater_valueAt(u, i),
            C4IndexUpdaterWrapper.ThreadSafetyLevel.Updater);

    public static bool c4indexupdater_setVectorAt(C4IndexUpdaterWrapper updater, ulong i, float* vector, 
        ulong dimension, C4Error* outError) =>
        updater.UseSafe(u => Native.c4indexupdater_setVectorAt(u, i, vector, dimension, outError),
            C4IndexUpdaterWrapper.ThreadSafetyLevel.Updater);

    public static bool c4indexupdater_skipVectorAt(C4IndexUpdaterWrapper updater, ulong i) =>
        updater.UseSafe(u => Native.c4indexupdater_skipVectorAt(u, i),
            C4IndexUpdaterWrapper.ThreadSafetyLevel.Updater);
}
//
// C4Collection_native_safe.cs
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
using System.Runtime.CompilerServices;

namespace LiteCore.Interop;

internal unsafe sealed class C4CollectionWrapper : NativeWrapper
{
    public delegate void NativeCallback(C4Collection* c);
    public delegate T NativeCallback<T>(C4Collection* c);

    public C4Collection* RawCollection => (C4Collection*)_nativeInstance;

    public readonly C4DatabaseWrapper Parent;

    // This wrapper shares a lock with its owning database
    public C4CollectionWrapper(C4Collection* c, C4DatabaseWrapper parent)
        : base((IntPtr)c, parent.InstanceSafety)
    {
        Parent = parent;
    }

    public T UseSafe<T>(NativeCallback<T> a)
    {
        using var scope = BeginLockedScope(true);
        return a(RawCollection);
    }

    protected override void Dispose(bool disposing)
    {
        Native.c4coll_release(RawCollection);
    }
}

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods

    public static C4CollectionWrapper? c4db_getDefaultCollection(C4DatabaseWrapper database, C4Error* outError)
    {
        var rawColl = Native.c4db_getDefaultCollection(database.RawDatabase, outError);
        if(rawColl == null) {
            return null;
        }

        return new C4CollectionWrapper(rawColl, database);
    }

    public static bool c4coll_isValid(C4CollectionWrapper collection)
    {
        return Native.c4coll_isValid(collection.RawCollection);
    }

    // Database Exclusive Methods

    public static bool c4db_hasScope(C4DatabaseWrapper database, string? name)
    {
        return database.UseSafe(db => Native.c4db_hasScope(db, name));
    }

    public static C4CollectionWrapper? c4db_getCollection(C4DatabaseWrapper database, C4CollectionSpec spec, C4Error* outError)
    {
        var nativeCollection = (C4Collection*)database.UseSafe(db => (IntPtr)Native.c4db_getCollection(db, spec, outError));
        if (nativeCollection == null) {
            return null;
        }

        return new C4CollectionWrapper(nativeCollection, database);
    }

    public static C4CollectionWrapper? c4db_createCollection(C4DatabaseWrapper database, C4CollectionSpec spec, C4Error* outError)
    {
        var nativeCollection = (C4Collection*)database.UseSafe(db => (IntPtr)Native.c4db_createCollection(db, spec, outError));
        if (nativeCollection == null) {
            return null;
        }

        return new C4CollectionWrapper(nativeCollection, database);
    }

    public static bool c4db_deleteCollection(C4DatabaseWrapper database, C4CollectionSpec spec, C4Error* outError)
    {
        return database.UseSafe(db => Native.c4db_deleteCollection(db, spec, outError));
    }

    public static FLMutableArray* c4db_collectionNames(C4DatabaseWrapper database, string? inScope, C4Error* outError)
    {
        return (FLMutableArray*)database.UseSafe(db => (IntPtr)Native.c4db_collectionNames(db, inScope, outError));
    }

    public static FLMutableArray* c4db_scopeNames(C4DatabaseWrapper database, C4Error* outError)
    {
        return (FLMutableArray*)database.UseSafe(db => (IntPtr)Native.c4db_scopeNames(db, outError));
    }

    public static ulong c4coll_getDocumentCount(C4CollectionWrapper collection)
    {
        return collection.UseSafe(Native.c4coll_getDocumentCount);
    }

    public static C4DocumentWrapper? c4coll_getDoc(C4CollectionWrapper collection, string? docID, bool mustExist, C4DocContentLevel content, C4Error* outError)
    {
        var rawDoc = (C4Document*)collection.UseSafe(c => (IntPtr)Native.c4coll_getDoc(c, docID, mustExist, content, outError));
        return rawDoc == null ? null : new C4DocumentWrapper(rawDoc, collection.InstanceSafety);
    }

    public static C4DocumentWrapper? c4coll_createDoc(C4CollectionWrapper collection, FLSlice docID, FLSlice body, C4RevisionFlags revisionFlags, C4Error* outError)
    {
        var rawDoc = (C4Document*)collection.UseSafe(c => (IntPtr)NativeRaw.c4coll_createDoc(c, docID, body, revisionFlags, outError));
        return rawDoc == null ? null : new C4DocumentWrapper(rawDoc, collection.InstanceSafety);
    }

    public static bool c4coll_purgeDoc(C4CollectionWrapper collection, string? docID, C4Error* outError)
    {
        return collection.UseSafe(c => Native.c4coll_purgeDoc(c, docID, outError));
    }

    public static bool c4coll_setDocExpiration(C4CollectionWrapper collection, string docID, long timestamp, C4Error* outError)
    {
        return collection.UseSafe(c => Native.c4coll_setDocExpiration(c, docID, timestamp, outError));
    }

    public static long c4coll_getDocExpiration(C4CollectionWrapper collection, string docID, C4Error* outError)
    {
        return collection.UseSafe(c => Native.c4coll_getDocExpiration(c, docID, outError));
    }
}
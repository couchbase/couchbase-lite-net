//
// C4Replicator_native_safe.cs
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

using System;
using System.Runtime.CompilerServices;

namespace LiteCore.Interop;

internal sealed unsafe class C4ReplicatorWrapper(C4Replicator* repl) : NativeWrapper((IntPtr)repl)
{
    public delegate void NativeCallback(C4Replicator* r);
    public delegate T NativeCallback<out T>(C4Replicator* r);

    public C4Replicator* RawReplicator => (C4Replicator*)_nativeInstance;

    public T UseSafe<T>(NativeCallback<T> a)
    {
        if (IsDisposed) {
            throw new ObjectDisposedException(nameof(C4ReplicatorWrapper));
        }

        using var scope = BeginLockedScope(true);
        return a(RawReplicator);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not C4ReplicatorWrapper other) {
            return false;
        }

        return other.RawReplicator == RawReplicator;
    }

    public override int GetHashCode() => (int)RawReplicator;

    protected override void Dispose(bool disposing) => Native.c4repl_free(RawReplicator);
}

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool c4address_fromURL(FLSlice url, C4Address* address, FLSlice* dbName) => 
        NativeRaw.c4address_fromURL(url, address, dbName);

    public static void c4repl_start(C4ReplicatorWrapper repl, bool reset) => Native.c4repl_start(repl.RawReplicator, reset);

    public static void c4repl_stop(C4ReplicatorWrapper repl) => Native.c4repl_stop(repl.RawReplicator);

    public static void c4repl_setHostReachable(C4ReplicatorWrapper repl, bool reachable) => 
        Native.c4repl_setHostReachable(repl.RawReplicator, reachable);

    public static void c4repl_setOptions(C4ReplicatorWrapper repl, FLSlice optionsDictFleece) => 
        NativeRaw.c4repl_setOptions(repl.RawReplicator, optionsDictFleece);

    public static C4ReplicatorStatus c4repl_getStatus(C4ReplicatorWrapper repl) => 
        Native.c4repl_getStatus(repl.RawReplicator);

    public static byte[]? c4repl_getPendingDocIDs(C4ReplicatorWrapper repl, C4CollectionSpec spec, C4Error* outError)
    {
        using var pendingIDs = Native.c4repl_getPendingDocIDs(repl.RawReplicator, spec, outError);
        return ((FLSlice)pendingIDs).ToArrayFast();
    }

    public static bool c4repl_isDocumentPending(C4ReplicatorWrapper repl, string docID, C4CollectionSpec spec, C4Error* outError) => 
        Native.c4repl_isDocumentPending(repl.RawReplicator, docID, spec, outError);

    public static void c4repl_setSuspended(C4ReplicatorWrapper repl, bool suspended) => 
        Native.c4repl_setSuspended(repl.RawReplicator, suspended);

    // Database Exclusive Methods

    public static C4ReplicatorWrapper? c4repl_new(C4DatabaseWrapper database, C4Address remoteAddress, string? remoteDatabaseName, C4ReplicatorParameters @params, string? logPrefix, C4Error* outError)
    {
        var rawRepl = (C4Replicator*)database.UseSafe(db =>
            (IntPtr)Native.c4repl_new(db, remoteAddress, remoteDatabaseName, @params, logPrefix, outError));
        return rawRepl == null ? null : new C4ReplicatorWrapper(rawRepl);
    }

    public static C4ReplicatorWrapper? c4repl_newLocal(C4DatabaseWrapper database, C4Database* otherLocalDB, C4ReplicatorParameters @params, string? logPrefix, C4Error* outError)
    {
        var rawRepl = (C4Replicator*)database.UseSafe(db =>
            (IntPtr)Native.c4repl_newLocal(db, otherLocalDB, @params, logPrefix, outError));
        return rawRepl == null ? null : new C4ReplicatorWrapper(rawRepl);
    }


    public static C4ReplicatorWrapper? c4repl_newWithSocket(C4DatabaseWrapper database, C4Socket* openSocket, C4ReplicatorParameters @params, string? logPrefix, C4Error* outError)
    {
        var rawRepl = (C4Replicator*)database.UseSafe(db =>
            (IntPtr)Native.c4repl_newWithSocket(db, openSocket, @params, logPrefix, outError));
        return rawRepl == null ? null : new C4ReplicatorWrapper(rawRepl);
    }

    public static bool c4db_setCookie(C4DatabaseWrapper database, string? setCookieHeader, string? fromHost, string? fromPath, bool acceptParentDomain, C4Error* outError) => 
        database.UseSafe(db => Native.c4db_setCookie(db, setCookieHeader, fromHost, fromPath, acceptParentDomain, outError));

    public static string? c4db_getCookies(C4DatabaseWrapper database, C4Address request, C4Error* outError) => 
        database.UseSafe(db => Native.c4db_getCookies(db, request, outError));

    // Replicator Exclusive

    public static bool c4repl_setProgressLevel(C4ReplicatorWrapper repl, C4ReplicatorProgressLevel level, C4Error* outError) => 
        repl.UseSafe(r => Native.c4repl_setProgressLevel(r, level, outError));
}
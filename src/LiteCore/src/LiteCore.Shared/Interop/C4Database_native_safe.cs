//
// C4Database_native_safe.cs
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

internal unsafe sealed class C4DatabaseWrapper : NativeWrapper
{
    public delegate T NativeCallback<T>(C4Database* db);

    public C4Database* RawDatabase => (C4Database*)_nativeInstance;

    public C4DatabaseWrapper(C4Database* db)
        : base((IntPtr)db)
    {

    }

    public static C4DatabaseWrapper Retained(C4Database* db)
    {
        return new C4DatabaseWrapper((C4Database *)Native.c4db_retain(db));
    }

    public T UseSafe<T>(NativeCallback<T> a)
    {
        using var scope = BeginLockedScope(true);
        return a(RawDatabase);
    }

    protected override void Dispose(bool disposing)
    {
        Native.c4db_release(RawDatabase);
    }
}

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods

    public static C4DatabaseWrapper? c4db_openNamed(string name, C4DatabaseConfig2* config, C4Error* outError)
    {
        var rawDb = Native.c4db_openNamed(name, config, outError);
        if(rawDb == null) {
            return null;
        }

        return new C4DatabaseWrapper(rawDb);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool c4db_copyNamed(string sourcePath, string destinationName, C4DatabaseConfig2* config, C4Error* outError)
    {
        return Native.c4db_copyNamed(sourcePath, destinationName, config, outError);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool c4db_deleteNamed(string dbName, string inDirectory, C4Error* outError)
    {
        return Native.c4db_deleteNamed(dbName, inDirectory, outError);
    }

    public static string? c4db_getPath(C4DatabaseWrapper database)
    {
        return Native.c4db_getPath(database.RawDatabase);
    }

    // Database Exclusive Methods

    public static bool c4db_close(C4DatabaseWrapper database, C4Error* outError)
    {
        return database.UseSafe(db => Native.c4db_close(db, outError));
    }

    public static bool c4db_getUUIDs(C4DatabaseWrapper database, C4UUID* publicUUID, C4UUID* privateUUID, C4Error* outError)
    {
        return database.UseSafe(db => Native.c4db_getUUIDs(db, publicUUID, privateUUID, outError));
    }

    public static bool c4db_maintenance(C4DatabaseWrapper database, C4MaintenanceType type, C4Error* outError)
    {
        return database.UseSafe(db => Native.c4db_maintenance(db, type, outError));
    }

    public static bool c4db_beginTransaction(C4DatabaseWrapper database, C4Error* outError)
    {
        return database.UseSafe(db => Native.c4db_beginTransaction(db, outError));
    }

    public static bool c4db_endTransaction(C4DatabaseWrapper database, bool commit, C4Error* outError)
    {
        return database.UseSafe(db => Native.c4db_endTransaction(db, commit, outError));
    }

    public static bool c4db_rekey(C4DatabaseWrapper database, C4EncryptionKey* newKey, C4Error* outError)
    {
        return database.UseSafe(db => Native.c4db_rekey(db, newKey, outError));
    }
}
//
// C4BlobStore_native_safe.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace LiteCore.Interop;

internal sealed unsafe class C4ReadStreamWrapper(C4ReadStream* readStream) : NativeWrapper((IntPtr)readStream)
{
    public delegate T NativeCallback<out T>(C4ReadStream* s);

    public C4ReadStream* RawStream => (C4ReadStream*)_nativeInstance;

    public T UseSafe<T>(NativeCallback<T> a)
    {
        using var scope = BeginLockedScope(true);
        return a(RawStream);
    }

    protected override void Dispose(bool disposing)
    {
        // Can't use super BeginLockedScope method because _disposed is already set to true by now
        using var scope = InstanceSafety.BeginLockedScope();
        Native.c4stream_close(RawStream);
    }
}

internal sealed unsafe class C4WriteStreamWrapper(C4WriteStream* readStream) : NativeWrapper((IntPtr)readStream)
{
    public delegate T NativeCallback<out T>(C4WriteStream* s);

    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public C4WriteStream* RawStream => (C4WriteStream*)_nativeInstance;

    public T UseSafe<T>(NativeCallback<T> a)
    {
        using var scope = BeginLockedScope(true);
        return a(RawStream);
    }

    protected override void Dispose(bool disposing)
    {
        // Can't use super BeginLockedScope method because _disposed is already set to true by now
        using var scope = InstanceSafety.BeginLockedScope();
        Native.c4stream_closeWriter(RawStream);
    }
}

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool c4blob_keyFromString(string? keyString, C4BlobKey* key) => Native.c4blob_keyFromString(keyString, key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? c4blob_keyToString(C4BlobKey key) => Native.c4blob_keyToString(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long c4blob_getSize(C4BlobStore* store, C4BlobKey key) => Native.c4blob_getSize(store, key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[]? c4blob_getContents(C4BlobStore* store, C4BlobKey key, C4Error* outError)
    {
        using var retVal = Native.c4blob_getContents(store, key, outError);
        return ((FLSlice)retVal).ToArrayFast();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool c4blob_create(C4BlobStore* store, byte[]? contents, 
        C4BlobKey* expectedKey, C4BlobKey* outKey, C4Error* outError) =>
        Native.c4blob_create(store, contents, expectedKey, outKey, outError);

    public static C4ReadStreamWrapper? c4blob_openReadStream(C4BlobStore *store, C4BlobKey key, 
        C4Error* outError)
    {
        var rawStream = Native.c4blob_openReadStream(store, key, outError);
        return rawStream == null ? null : new C4ReadStreamWrapper(rawStream);
    }

    public static ulong c4stream_read(C4ReadStreamWrapper stream, byte[] buffer, C4Error* outError) => 
        stream.UseSafe(s => Native.c4stream_read(s, buffer, outError));

    public static long c4stream_getLength(C4ReadStreamWrapper stream, C4Error* outError) => 
        Native.c4stream_getLength(stream.RawStream, outError);

    public static C4WriteStreamWrapper? c4blob_openWriteStream(C4BlobStore* store, C4Error* outError)
    {
        var rawStream = Native.c4blob_openWriteStream(store, outError);
        return rawStream == null ? null : new C4WriteStreamWrapper(rawStream);
    }

    // Database Exclusive Methods

    public static C4BlobStore* c4db_getBlobStore(C4DatabaseWrapper database, C4Error* outError) => 
        (C4BlobStore*)database.UseSafe(db => (IntPtr)Native.c4db_getBlobStore(db, outError));

    // Stream Exclusive Methods

    public static bool c4stream_seek(C4ReadStreamWrapper stream, ulong position, C4Error* outError) => 
        stream.UseSafe(s => Native.c4stream_seek(s, position, outError));

    public static bool c4stream_write(C4WriteStreamWrapper stream, byte[] bytes, C4Error* outError) => 
        stream.UseSafe(s => Native.c4stream_write(s, bytes, outError));

    public static C4BlobKey c4stream_computeBlobKey(C4WriteStreamWrapper stream) => 
        stream.UseSafe(Native.c4stream_computeBlobKey);

    public static bool c4stream_install(C4WriteStreamWrapper stream, C4BlobKey* expectedKey, C4Error* outError) => 
        stream.UseSafe(s => Native.c4stream_install(s, expectedKey, outError));
}
//
// C4Query_native_safe.cs
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
using System.Linq;

namespace LiteCore.Interop;

internal sealed unsafe class C4QueryWrapper(C4Query* query, ThreadSafety threadSafety) : NativeWrapper((IntPtr)query)
{
    public delegate void NativeCallback(C4Query* q);
    public delegate T NativeCallback<out T>(C4Query* q);

    [Flags]
    public enum ThreadSafetyLevel
    {
        Query = 1 << 0,
        Database = 1 << 1
    }

    public C4Query* RawQuery => (C4Query*)_nativeInstance;

    public readonly ThreadSafety DatabaseThreadSafety = threadSafety;

    public void UseSafe(NativeCallback a, ThreadSafetyLevel safetyLevel)
    {
        var withInstance = safetyLevel.HasFlag(ThreadSafetyLevel.Query);
        var additional = safetyLevel.HasFlag(ThreadSafetyLevel.Database) ?
            Enumerable.Repeat(DatabaseThreadSafety, 1) : [];

        using var scope = BeginLockedScope(withInstance, additional.ToArray());
        a(RawQuery);
    }

    public T UseSafe<T>(NativeCallback<T> a, ThreadSafetyLevel safetyLevel)
    {
        var withInstance = safetyLevel.HasFlag(ThreadSafetyLevel.Query);
        var additional = safetyLevel.HasFlag(ThreadSafetyLevel.Database) ?
            Enumerable.Repeat(DatabaseThreadSafety, 1) : [];

        using var scope = BeginLockedScope(withInstance, additional.ToArray());
        return a(RawQuery);
    }

    protected override void Dispose(bool disposing) => Native.c4query_release(RawQuery);
}

internal sealed unsafe class C4QueryEnumeratorWrapper(C4QueryEnumerator* queryEnum, ThreadSafety threadSafety) : NativeWrapper((IntPtr)queryEnum, threadSafety)
{
    public delegate void NativeCallback(C4QueryEnumerator* e);
    public delegate T NativeCallback<out T>(C4QueryEnumerator* e);

    public C4QueryEnumerator* RawEnumerator => (C4QueryEnumerator*)_nativeInstance;

    public T UseSafe<T>(NativeCallback<T> a)
    {
        using var scope = BeginLockedScope(true);
        return a(RawEnumerator);
    }

    public void UseSafe(NativeCallback a)
    {
        using var scope = BeginLockedScope(true);
        a(RawEnumerator);
    }

    protected override void Dispose(bool disposing) => Native.c4queryenum_release(RawEnumerator);
}

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods

    public static void c4query_setParameters(C4QueryWrapper query, string? encodedParameters) => 
        Native.c4query_setParameters(query.RawQuery, encodedParameters);

    // Database Exclusive Methods

    public static C4QueryWrapper? c4query_new2(C4DatabaseWrapper database, C4QueryLanguage language, string? expression, int* outErrorPos, C4Error* outError)
    {
        var rawQuery = (C4Query*)database.UseSafe(db => (IntPtr)Native.c4query_new2(db, language, expression, outErrorPos, outError));
        return rawQuery == null ? null : new C4QueryWrapper(rawQuery, database.InstanceSafety);

    }

    public static string? c4query_explain(C4QueryWrapper query) =>
        query.UseSafe(Native.c4query_explain,
            C4QueryWrapper.ThreadSafetyLevel.Database);

    public static C4QueryEnumeratorWrapper? c4query_run(C4QueryWrapper query, FLSlice encodedParameters, C4Error* outError)
    {
        var rawEnum = (C4QueryEnumerator*)query.UseSafe(q =>
            (IntPtr)NativeRaw.c4query_run(q, encodedParameters, outError), C4QueryWrapper.ThreadSafetyLevel.Database);
        return rawEnum == null ? null : new C4QueryEnumeratorWrapper(rawEnum, query.DatabaseThreadSafety);
    }

    public static C4QueryEnumeratorWrapper? c4queryenum_refresh(C4QueryEnumeratorWrapper enumerator, C4Error* outError)
    {
        var rawEnum = (C4QueryEnumerator*)enumerator.UseSafe(e => (IntPtr)Native.c4queryenum_refresh(e, outError));
        return rawEnum == null ? null : new C4QueryEnumeratorWrapper(rawEnum, enumerator.InstanceSafety);
    }

    // Query exclusive

    public static uint c4query_columnCount(C4QueryWrapper query) =>
        query.UseSafe(Native.c4query_columnCount,
            C4QueryWrapper.ThreadSafetyLevel.Query);

    public static FLSlice c4query_columnTitle(C4QueryWrapper query, uint column) =>
        query.UseSafe(q => Native.c4query_columnTitle(q, column),
            C4QueryWrapper.ThreadSafetyLevel.Query);

    public static bool c4queryenum_next(C4QueryEnumeratorWrapper enumerator, C4Error* outError) => 
        enumerator.UseSafe(e => Native.c4queryenum_next(e, outError));

    public static bool c4queryenum_seek(C4QueryEnumeratorWrapper enumerator, long rowIndex, C4Error* outError) => 
        enumerator.UseSafe(e => Native.c4queryenum_seek(e, rowIndex, outError));
}
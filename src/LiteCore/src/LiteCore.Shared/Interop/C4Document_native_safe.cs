//
// C4Document_native_safe.cs
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
using Couchbase.Lite.Util;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LiteCore.Interop;

// NOTE: This was an existing class before NativeWrapper existed and
// is sort of coupled to the implementation at the moment so that's
// why it looks different than the others
internal sealed unsafe class C4DocumentWrapper : NativeWrapper
{
    public delegate T NativeCallback<T>(C4Document* doc);

    [Flags]
    public enum ThreadSafetyLevel
    {
        Document = 1 << 0,
        Database = 1 << 1,
        Full = Document | Database
    }

    #region Constants

    private const string Tag = nameof(C4DocumentWrapper);

    #endregion

    #region Variables

    public readonly C4Document* RawDoc;
    public readonly ThreadSafety DatabaseThreadSafety;

    #endregion

    #region Properties

    public bool HasValue => RawDoc != null;

    #endregion

    #region Constructors

    public C4DocumentWrapper(C4Document* doc, ThreadSafety databaseThreadSafety)
        : base((IntPtr)doc)
    {
        RawDoc = doc;
        if (RawDoc == null) {
            GC.SuppressFinalize(this);
        }

        DatabaseThreadSafety = databaseThreadSafety;
    }

    #endregion

    public T UseSafe<T>(NativeCallback<T> a, ThreadSafetyLevel safetyLevel)
    {
        var withInstance = safetyLevel.HasFlag(ThreadSafetyLevel.Document);
        var additional = safetyLevel.HasFlag(ThreadSafetyLevel.Database) ?
            Enumerable.Repeat(DatabaseThreadSafety, 1) : Enumerable.Empty<ThreadSafety>();

        using var scope = BeginLockedScope(withInstance, additional.ToArray());
        return a(RawDoc);
    }

    #region Overrides

    protected override void Dispose(bool disposing)
    {
        Native.c4doc_release(RawDoc);
    }

    public override bool Equals(object? obj)
    {
        if (!(obj is C4DocumentWrapper other)) {
            return false;
        }

        return other.RawDoc == RawDoc;
    }

    public override int GetHashCode()
    {
        return (int)RawDoc;
    }

    public override string ToString()
    {
        return RawDoc == null ? "<empty>" : $"C4Document -> {RawDoc->docID.CreateString()}";
    }

    #endregion
}

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint c4rev_getGeneration(FLSlice revID)
    {
        return NativeRaw.c4rev_getGeneration(revID);
    }

    // Document Exclusive Methods

    public static FLSlice c4doc_getRevisionBody(C4DocumentWrapper doc)
    {
        return doc.UseSafe(NativeRaw.c4doc_getRevisionBody,
            C4DocumentWrapper.ThreadSafetyLevel.Document);
    }

    public static bool c4doc_selectNextLeafRevision(C4DocumentWrapper doc, bool includeDeleted, bool withBody, C4Error* outError)
    {
        return doc.UseSafe(d => Native.c4doc_selectNextLeafRevision(d, includeDeleted, withBody, outError),
            C4DocumentWrapper.ThreadSafetyLevel.Document);
    }

    public static bool c4doc_resolveConflict(C4DocumentWrapper doc, FLSlice winningRevID, FLSlice losingRevID, FLSlice mergedBody, C4RevisionFlags mergedFlags, C4Error* outError)
    {
        return doc.UseSafe(d => NativeRaw.c4doc_resolveConflict(d, winningRevID, losingRevID, mergedBody, mergedFlags, outError),
            C4DocumentWrapper.ThreadSafetyLevel.Document);
    }

    // Document & Database Exclusive Methods

    public static bool c4doc_save(C4DocumentWrapper doc, uint maxRevTreeDepth, C4Error* outError)
    {
        return doc.UseSafe(d => Native.c4doc_save(d, maxRevTreeDepth, outError),
            C4DocumentWrapper.ThreadSafetyLevel.Full);
    }

    public static C4DocumentWrapper? c4doc_update(C4DocumentWrapper doc, FLSlice revisionBody, C4RevisionFlags revisionFlags, C4Error* outError)
    {
        var rawDoc = (C4Document*)doc.UseSafe(d => (IntPtr)NativeRaw.c4doc_update(d, revisionBody, revisionFlags, outError),
            C4DocumentWrapper.ThreadSafetyLevel.Full);
        if(rawDoc == null) {
            return null;
        }

        return new C4DocumentWrapper(rawDoc, doc.DatabaseThreadSafety);
    }
}
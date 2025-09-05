// 
//  Document.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite;

/// <summary>
/// An extension class for helping to turn a nanosecond based timestamp into a
/// <see cref="DateTimeOffset"/> object
/// </summary>
public static class TimestampExtensions
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
    /// <summary>
    /// Converts the nanosecond timestamp to a DateTimeOffset in UTC time
    /// </summary>
    /// <param name="rawVal">The nanosecond timestamp</param>
    /// <returns>The DateTimeOffset object using the timestamp, or null if it was invalid</returns>
    public static DateTimeOffset? AsDateTimeOffset(this ulong rawVal)
    {
        if(rawVal == 0) {
            return null;
        }

        // .NET ticks are in 100 nanosecond intervals
        return UnixEpoch + TimeSpan.FromTicks((long)(rawVal / 100));
    }
}
/// <summary>
/// A class representing a document which cannot be altered
/// </summary>
public unsafe class Document : IDictionaryObject, IJSON, IDisposable
{
    private readonly string? _revId;
    private readonly DisposalWatchdog _disposalWatchdog;
    private readonly ThreadSafety _threadSafety = new();

    private C4DocumentWrapper? _c4Doc;
    private FLDict* _data;
    private MRoot? _root;
    
    /// <summary>
    /// The backing dictionary for this document
    /// </summary>
    protected IDictionaryObject? _dict;

    internal C4DatabaseWrapper C4Db
    {
        get {
            var retVal = Database?.C4db;
            Debug.Assert(retVal != null);
 #pragma warning disable CS8603 // Possible null reference return.
            return retVal;
 #pragma warning restore CS8603 // Possible null reference return.
        }
    }

    private C4CollectionWrapper C4Coll
    {
        get {
            Debug.Assert(Collection != null);
            return Collection!.C4Coll;
        }
    }

    internal C4DocumentWrapper? C4Doc
    {
        get => _c4Doc;
        private set {
            using var threadSafetyScope = _threadSafety.BeginLockedScope();
            Misc.SafeSwap(ref _c4Doc, value);

            _data = null;

            if (value?.HasValue == true) {
                _data = NativeSafe.c4doc_getProperties(value);
            }

            UpdateDictionary();
        }
    }

    /// <summary>
    /// Gets the database that this document belongs to, if any
    /// </summary>
    internal Database? Database => Collection?.Database;

    internal string? RevisionIDs
    {
        get {
            using var scope = _threadSafety.BeginLockedScope();
            if(C4Doc == null) {
                return null;
            }
            var fullC4Doc = LiteCoreBridge.CheckTyped(err => NativeSafe.c4coll_getDoc(C4Coll, Id, true, C4DocContentLevel.DocGetAll, err))!;
            return NativeSafe.c4doc_getRevisionHistory(fullC4Doc);
        }    
    }

    /// <summary>
    /// Gets the Collection that this document belongs to, if any
    /// </summary>
    public Collection? Collection { get; set; }

    internal bool Exists
    {
        get {
            using var threadSafetyScope = _threadSafety.BeginLockedScope();
            return C4Doc?.HasValue == true && C4Doc.RawDoc->flags.HasFlag(C4DocumentFlags.DocExists);
        }
    }

    /// <summary>
    /// Gets this document's unique ID
    /// </summary>
    public string Id { get; }

    internal bool IsDeleted
    {
        get {
            using var threadSafetyScope = _threadSafety.BeginLockedScope();
            return C4Doc?.HasValue == true && C4Doc.RawDoc->selectedRev.flags.HasFlag(C4RevisionFlags.Deleted);
        }
    }

    internal bool IsEmpty => _dict?.Count == 0;

    private protected virtual bool IsMutable => false;

    /// <summary>
    /// The RevisionID in Document class is a constant, while the RevisionID in <see cref="MutableDocument" /> class is not.
    /// Newly created document will have a null RevisionID. The RevisionID in <see cref="MutableDocument" /> will be updated on save.
    /// The RevisionID format is opaque, which means it's format has no meaning and shouldn't be parsed to get information.
    /// </summary>
    public string? RevisionID
    {
        get {
            using var threadSafetyScope = _threadSafety.BeginLockedScope();
            return C4Doc?.HasValue == true ? C4Doc.RawDoc->selectedRev.revID.CreateString() : _revId;
        }
    }

    /// <summary>
    /// The hybrid logical timestamp that the revision was created, represented in nanoseconds
    /// from the unix epoch.  If you want this value as a DateTimeOffset you can use the
    /// convenience function <see cref="TimestampExtensions.AsDateTimeOffset(ulong)">AsDateTimeOffset</see>.
    /// Just be aware that DateTimeOffset only handles 100 nanosecond resolution.
    /// </summary>
    public ulong Timestamp
    {
        get {
            using var scope = _threadSafety.BeginLockedScope();
            return C4Doc?.HasValue == true ? NativeRaw.c4rev_getTimestamp(C4Doc.RawDoc->selectedRev.revID) : 0;
        }
    }

    /// <summary>
    /// Gets the sequence of this document (a unique incrementing number
    /// identifying its status in a database)
    /// </summary>
    public ulong Sequence
    {
        get {
            using var threadSafetyScope = _threadSafety.BeginLockedScope();
            return C4Doc?.HasValue == true ? C4Doc.RawDoc->selectedRev.sequence : 0UL;
        }
    }

    /// <inheritdoc />
    public IFragment this[string key] => 
        _dict == null 
            ? throw new InvalidOperationException("Null _dict when trying to access key value") 
            : _dict[key];

    /// <inheritdoc />
    public ICollection<string> Keys => 
        _dict?.Keys ?? throw new InvalidOperationException("Null _dict when trying to access Keys");

    /// <inheritdoc />
    public int Count => _dict?.Count ?? 0;

    internal Document(Collection? collection, string id, C4DocumentWrapper? c4Doc)
    {
        Collection = collection;
        Id = id;
        C4Doc = c4Doc;
        _disposalWatchdog = new DisposalWatchdog(GetType().Name);
    }

    internal Document(Collection? collection, string id, C4DocContentLevel contentLevel = C4DocContentLevel.DocGetCurrentRev)
        : this(collection, id, null)
    {
        C4Doc = NativeHandler.Create().AllowError(new C4Error(C4ErrorCode.NotFound)).Execute(
            err => NativeSafe.c4coll_getDoc(C4Coll, id, true, contentLevel, err));
    }

    internal Document(Collection? collection, string id, string revId, FLDict* body)
        : this(collection, id, null)
    {
        _data = body;
        UpdateDictionary();
        _revId = revId;
    }

    /// <summary>
    /// Creates a mutable version of a document (i.e. one that
    /// can be edited)
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// InvalidOperationException thrown when trying edit Documents from a replication filter.
    /// </exception>
    /// <returns>A mutable version of the document</returns>
    public virtual MutableDocument ToMutable() => 
        _revId != null 
            ? throw new InvalidOperationException(CouchbaseLiteErrorMessage.NoDocEditInReplicationFilter) 
            : new MutableDocument(this);

    internal virtual FLSliceResult Encode()
    {
        _disposalWatchdog.CheckDisposed();
        if (C4Doc?.HasValue != true) {
            return (FLSliceResult)FLSlice.Null;
        }
        
        var data = NativeSafe.c4doc_getRevisionBody(C4Doc);
        return Native.FLSlice_Copy(data);
    }

    internal void ReplaceC4Doc(C4DocumentWrapper newDoc)
    {
        using var threadSafetyScope = _threadSafety.BeginLockedScope();
        Misc.SafeSwap(ref _c4Doc, newDoc);
    }

    internal bool SelectConflictingRevision()
    {
        if (_c4Doc == null) {
            throw new InvalidOperationException(CouchbaseLiteErrorMessage.NoDocumentRevision);
        }
            
        var foundConflict = false;
        var err = new C4Error();
        while (!foundConflict && NativeSafe.c4doc_selectNextLeafRevision(_c4Doc, true, true, &err)) {
            foundConflict = _c4Doc.RawDoc->selectedRev.flags.HasFlag(C4RevisionFlags.IsConflict);
        }

        if (err.code != 0) {
            throw CouchbaseException.Create(err);
        }

        if (foundConflict) {
            // HACK: Side effect of updating data
            C4Doc = _c4Doc.Retain<C4DocumentWrapper>();
        }
            
        return foundConflict;
    }
    
    private void UpdateDictionary()
    {
        if (_data != null) {
            Debug.Assert(Database != null);
            Misc.SafeSwap(ref _root,
                new MRoot(new DocContext(Database!, _c4Doc), (FLValue*) _data, IsMutable));

            // TODO: Is this needed?
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            using var threadSafetyScope = Collection?.ThreadSafety?.BeginLockedScope();
            _dict = (DictionaryObject?) _root!.AsObject();
        } else {
            Misc.SafeSwap(ref _root, null);
            _dict = IsMutable ? new InMemoryDictionary() : new DictionaryObject();
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var id = new SecureLogString(Id, LogMessageSensitivity.PotentiallyInsecure);
        return $"{GetType().Name}[{id}]";
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, RevisionID);

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is not Document d) {
            return false;
        }

        // First check the collection and database match
        // This needs to be done in a less strict way than
        // normal comparison because we don't care as much that
        // the exact instances are the same, just that they refer
        // to the same on-disk entities
        if(Database?.Path != d.Database?.Path || Collection?.FullName != d.Collection?.FullName) {
            return false;
        }
            
        // Next check the ID
        if (Id != d.Id) {
            return false;
        }

        // Do a quick check that the actual keys are the same
        var commonCount = Keys.Intersect(d.Keys).Count();
        if (commonCount != Keys.Count || commonCount != d.Keys.Count) {
            return false; // The set of keys is different
        }

        // Final fallback, examine every key and value
        return !(from key in Keys 
            let left = GetValue(key) 
            let right = d.GetValue(key) 
            where !left.RecursiveEqual(right)
            select left).Any();
    }

    /// <inheritdoc />
    public bool Contains(string key) => _dict?.Contains(key) == true;

    /// <inheritdoc />
    public ArrayObject? GetArray(string key) => _dict?.GetArray(key);

    /// <inheritdoc />
    public Blob? GetBlob(string key) => _dict?.GetBlob(key);

    /// <inheritdoc />
    public bool GetBoolean(string key) => _dict?.GetBoolean(key) ?? false;

    /// <inheritdoc />
    public DateTimeOffset GetDate(string key) => _dict?.GetDate(key) ?? DateTimeOffset.MinValue;

    /// <inheritdoc />
    public DictionaryObject? GetDictionary(string key) => _dict?.GetDictionary(key);

    /// <inheritdoc />
    public double GetDouble(string key) => _dict?.GetDouble(key) ?? 0.0;

    /// <inheritdoc />
    public float GetFloat(string key) => _dict?.GetFloat(key) ?? 0.0f;

    /// <inheritdoc />
    public int GetInt(string key) => _dict?.GetInt(key) ?? 0;

    /// <inheritdoc />
    public long GetLong(string key) => _dict?.GetLong(key) ?? 0L;

    /// <inheritdoc />
    public object? GetValue(string key) => _dict?.GetValue(key);

    /// <inheritdoc />
    public string? GetString(string key) => _dict?.GetString(key);

    /// <inheritdoc />
    public Dictionary<string, object?> ToDictionary() => _dict?.ToDictionary() ?? new Dictionary<string, object?>();

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        using var threadSafetyScope = _threadSafety.BeginLockedScope();
        _disposalWatchdog.Dispose();
        _root?.Dispose();
        Misc.SafeSwap(ref _c4Doc, null);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _dict?.GetEnumerator() ?? new InMemoryDictionary().GetEnumerator();

    /// <inheritdoc />
    public string ToJSON()
    {
        if(IsMutable) {
            throw new NotSupportedException();
        }

        if (C4Doc != null) {
            // This will throw if null, so ! is safe
            return LiteCoreBridge.Check(err =>
                NativeSafe.c4doc_bodyAsJSON(C4Doc, true, err))!;
        }
        
        WriteLog.To.Database.E("Document", "c4Doc null in ToJSON()");
        return "";
    }
}
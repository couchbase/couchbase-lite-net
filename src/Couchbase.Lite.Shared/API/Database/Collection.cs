// 
//  Collection.cs
// 
//  Copyright (c) 2022 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text.Json;

namespace Couchbase.Lite;

/// <summary>
/// A class representing a Couchbase Lite collection.  A collection is a logical group
/// of documents segregated in a domain specific way inside a <see cref="Scope"/>.
/// It is comparable to a SQL table in a document based database world.
/// </summary>
public sealed unsafe class Collection : IChangeObservable<CollectionChangedEventArgs>, IDocumentChangeObservable,
    IDisposable
{
    private const string Tag = nameof(Collection);

    /// <summary>
    /// The name of the default <see cref="Scope"/> that exists in every <see cref="Database"/>
    /// </summary>
    public static readonly string DefaultScopeName = Database.DefaultScopeName;

    /// <summary>
    /// The name of the default Collection that exists in every <see cref="Scope"/>
    /// </summary>
    public static readonly string DefaultCollectionName = Database.DefaultCollectionName;

    private static readonly C4DocumentObserverCallback DocumentObserverCallback = DoDocObserverCallback;
    private static readonly C4CollectionObserverCallback DatabaseObserverCallback = DoDbObserverCallback;

    private C4CollectionWrapper? _c4Coll;

    private GCHandle _obsContext;

    private C4CollectionObserver* _obs;

    private readonly FilteredEvent<string, DocumentChangedEventArgs> _documentChanged =
        new FilteredEvent<string, DocumentChangedEventArgs>();

    private readonly Dictionary<string, Tuple<IntPtr, GCHandle>> _docObs = new Dictionary<string, Tuple<IntPtr, GCHandle>>();

    private readonly Event<CollectionChangedEventArgs> _databaseChanged =
        new Event<CollectionChangedEventArgs>();

    private readonly TaskFactory _callbackFactory = new TaskFactory(new QueueTaskScheduler());

    // Must be called inside self lock
    [MemberNotNullWhen(false, nameof(C4Db), nameof(_c4Coll))]
    private bool IsClosed => C4Db == null || _c4Coll == null || !NativeSafe.c4coll_isValid(_c4Coll);

    // Must be called inside self lock
    internal bool IsValid => _c4Coll != null && NativeSafe.c4coll_isValid(_c4Coll);

    internal C4DatabaseWrapper? C4Db => Database.C4db;

    internal C4CollectionWrapper C4Coll
    {
        get { 
            if (_c4Coll == null) 
                throw new ObjectDisposedException(String.Format(CouchbaseLiteErrorMessage.CollectionNotAvailable,
                    ToString())); 

            return _c4Coll; 
        }
    }

    internal ThreadSafety ThreadSafety { get; }

    /// <summary>
    /// Gets the database that this collection belongs to
    /// </summary>
    public Database Database { get; internal set; }

    /// <summary>
    /// Gets the Collection Name
    /// </summary>
    /// <remarks>
    /// Naming rules:
    /// Must be between 1 and 251 characters in length.
    /// Can only contain the characters A-Z, a-z, 0-9, and the symbols _, -, and %. 
    /// Cannot start with _ or %.
    /// Case-sensitive.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets the Collection Full Name
    /// <remark>
    /// The format of the collection's full name is {scope-name}.{collection-name}.
    /// </remark>
    /// </summary>
    public string FullName => Scope.Name + "." + Name;

    /// <summary>
    /// Gets the Scope of the Collection belongs to
    /// </summary>
    public Scope Scope { get; }

    /// <summary>
    /// Gets the total documents in the Collection
    /// </summary>
    public ulong Count => NativeSafe.c4coll_getDocumentCount(C4Coll);

    /// <summary>
    /// Gets a <see cref="DocumentFragment"/> with the given document ID
    /// </summary>
    /// <param name="id">The ID of the <see cref="DocumentFragment"/> to retrieve</param>
    /// <returns>The <see cref="DocumentFragment"/> object</returns>
    public DocumentFragment this[string id] => new DocumentFragment(GetDocument(id));

    internal Collection(Database database, string name, Scope scope, C4CollectionWrapper c4Coll)
    {
        Database = database;
        ThreadSafety = database.ThreadSafety;

        Name = name;
        Scope = scope;

        _c4Coll = c4Coll.Retain<C4CollectionWrapper>();
    }

    /// <summary>
    /// Finalizer
    /// </summary>
    ~Collection()
    {
        try {
            Dispose(false);
        } catch (Exception e) {
            WriteLog.To.Database.E(Tag, "Error during finalizer, swallowing!", e);
        }
    }

    /// <summary>
    /// Adds a change listener for the changes that occur in this collection.  Signatures
    /// are the same as += style event handlers, but the callbacks will be called using the
    /// specified <see cref="TaskScheduler"/>.  If the scheduler is null, the default task
    /// scheduler will be used (scheduled via thread pool).
    /// </summary>
    /// <param name="scheduler">The scheduler to use when firing the change handler</param>
    /// <param name="handler">The handler to invoke</param>
    /// <returns>A <see cref="ListenerToken"/> that can be used to remove the handler later</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c></exception>
    /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
    public ListenerToken AddChangeListener(TaskScheduler? scheduler, EventHandler<CollectionChangedEventArgs> handler)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(handler), handler);

        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();

        var cbHandler = new CouchbaseEventHandler<CollectionChangedEventArgs>(handler, scheduler);
        if (_databaseChanged.Add(cbHandler) == 0) {
            _obsContext = GCHandle.Alloc(this);
            _obs = (C4CollectionObserver*)LiteCoreBridge.Check(err => NativeSafe.c4dbobs_createOnCollection(C4Coll, DatabaseObserverCallback, GCHandle.ToIntPtr(_obsContext).ToPointer(), err));
        }

        return new ListenerToken(cbHandler, ListenerTokenType.Database, this);
    }

    /// <summary>
    /// Adds a change listener for the changes that occur in this collection.  Signatures
    /// are the same as += style event handlers.  The callback will be invoked on a thread pool
    /// thread.
    /// </summary>
    /// <param name="handler">The handler to invoke</param>
    /// <returns>A <see cref="ListenerToken"/> that can be used to remove the handler later</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c></exception>
    /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
    public ListenerToken AddChangeListener(EventHandler<CollectionChangedEventArgs> handler) => AddChangeListener(null, handler);

    /// <summary>
    /// Adds a document change listener for the document with the given ID and the <see cref="TaskScheduler"/>
    /// that will be used to invoke the callback.  If the scheduler is not specified, then the default scheduler
    /// will be used (scheduled via thread pool)
    /// </summary>
    /// <param name="id">The document ID</param>
    /// <param name="scheduler">The scheduler to use when firing the event handler</param>
    /// <param name="handler">The logic to handle the event</param>
    /// <returns>A <see cref="ListenerToken"/> that can be used to remove the listener later</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> or <paramref name="id"/>
    /// is <c>null</c></exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/> if this method 
    /// is called after the collection is closed</exception>
    public ListenerToken AddDocumentChangeListener(string id, TaskScheduler? scheduler, EventHandler<DocumentChangedEventArgs> handler)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(id), id);
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(handler), handler);

        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();

        var cbHandler =
            new CouchbaseEventHandler<string, DocumentChangedEventArgs>(handler, id, scheduler);
        var count = _documentChanged.Add(cbHandler);
        if (count == 0) {
            var handle = GCHandle.Alloc(this);
            var docObs = (C4DocumentObserver*)LiteCoreBridge.Check(err => NativeSafe.c4docobs_createWithCollection(C4Coll, id, DocumentObserverCallback, GCHandle.ToIntPtr(handle).ToPointer(), err));
            _docObs[id] = Tuple.Create((IntPtr)docObs, handle);
        }

        return new ListenerToken(cbHandler, ListenerTokenType.Document, this);
    }

    /// <summary>
    /// Adds a document change listener for the document with the given ID. The callback will be invoked on a thread pool thread.
    /// </summary>
    /// <param name="id">The document ID</param>
    /// <param name="handler">The logic to handle the event</param>
    /// <returns>A <see cref="ListenerToken"/> that can be used to remove the listener later</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> or <paramref name="id"/>
    /// is <c>null</c></exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/> if this method 
    /// is called after the collection is closed</exception>
    public ListenerToken AddDocumentChangeListener(string id, EventHandler<DocumentChangedEventArgs> handler) => AddDocumentChangeListener(id, null, handler);

    /// <summary>
    /// Removes a collection changed listener by token
    /// </summary>
    /// <param name="token">The token received from <see cref="AddChangeListener(TaskScheduler, EventHandler{CollectionChangedEventArgs})"/>
    /// and family</param>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/> if this method 
    /// is called after the collection is closed</exception>
    public void RemoveChangeListener(ListenerToken token)
    {
        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();

        if (token.Type == ListenerTokenType.Database) {
            if (_databaseChanged.Remove(token) == 0) {
                Native.c4dbobs_free(_obs);
                _obs = null;
                if (_obsContext.IsAllocated) {
                    _obsContext.Free();
                }
            }
        } else {
            if (_documentChanged.Remove(token, out var docID) == 0) {
                // docID is guaranteed non-null if return of Remove is 0 or higher
                if (_docObs.TryGetValue(docID!, out var observer)) {
                    _docObs.Remove(docID!);
                    Native.c4docobs_free((C4DocumentObserver*)observer.Item1);
                    observer.Item2.Free();
                }
            }
        }
    }

    /// <summary>
    /// Deletes a document from the collection.  When write operations are executed
    /// concurrently, the last writer will overwrite all other written values.
    /// Calling this method is the same as calling <see cref="Delete(Document, ConcurrencyControl)"/>
    /// with <see cref="ConcurrencyControl.LastWriteWins"/>
    /// </summary>
    /// <param name="document">The document</param>
    /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.InvalidParameter"/>
    /// when trying to save a document into a collection other than the one it was previously added to</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotFound"/>
    /// when trying to delete a document that hasn't been saved into a <see cref="Collection"/> yet</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public void Delete(Document document) => Delete(document, ConcurrencyControl.LastWriteWins);

    /// <summary>
    /// Deletes the given <see cref="Document"/> from this collection
    /// </summary>
    /// <param name="document">The document to save</param>
    /// <param name="concurrencyControl">The rule to use when encountering a conflict in the collection</param>
    /// <returns><c>true</c> if the delete operation succeeded, <c>false</c> if there was a conflict</returns>
    /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.InvalidParameter"/>
    /// when trying to save a document into a collection other than the one it was previously added to</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotFound"/>
    /// when trying to delete a document that hasn't been saved into a <see cref="Collection"/> yet</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public bool Delete(Document document, ConcurrencyControl concurrencyControl)
    {
        var doc = CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(document), document);
        return Save(doc, null, concurrencyControl, true);
    }

    /// <summary>
    /// Gets the <see cref="Document"/> with the specified ID
    /// </summary>
    /// <param name="id">The ID to use when creating or getting the document</param>
    /// <returns>The instantiated document, or <c>null</c> if it does not exist</returns>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public Document? GetDocument(string id)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(id), id);

        using var scope = ThreadSafety.BeginLockedScope();
        return GetDocumentInternal(id);
    }

    /// <summary>
    /// Gets an existing index in a collection by name.
    /// </summary>
    /// <param name="name">The name of the index to retrieve.</param>
    /// <returns>The index object, or <c>null</c> if nonexistent</returns>
    public IQueryIndex? GetIndex(string name)
    {
        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();
        var index = NativeHandler.Create()
            .AllowError(new C4Error(C4ErrorCode.MissingIndex))
            .Execute(err => NativeSafe.c4coll_getIndex(_c4Coll, name, err));

        return index == null ? null : new QueryIndexImpl(index, this, name);
    }

    /// <summary>
    /// Purges the given <see cref="Document"/> from the collection.  This leaves
    /// no trace behind and will not be replicated
    /// </summary>
    /// <param name="document">The document to purge</param>
    /// <exception cref="InvalidOperationException">Thrown when trying to purge a document from a collection
    /// other than the one it was previously added to</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public void Purge(Document document)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(document), document);
        using var scope = ThreadSafety.BeginLockedScope();
        VerifyCollection(document);

        if (!document.Exists) {
            throw new CouchbaseLiteException(C4ErrorCode.NotFound);
        }

        Database.InBatch(() => PurgeDocById(document.Id));
    }

    /// <summary>
    /// Purges the given document id of the <see cref="Document"/> 
    /// from the collection.  This leaves no trace behind and will 
    /// not be replicated
    /// </summary>
    /// <param name="docId">The id of the document to purge</param>
    /// <exception cref="C4ErrorCode.NotFound">Throws NOT FOUND error if the document 
    /// of the docId doesn't exist.</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public void Purge(string docId)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(docId), docId);
        Database.InBatch(() => PurgeDocById(docId));
    }

    /// <summary>
    /// Saves the given <see cref="MutableDocument"/> into this collection.  This call is equivalent to calling
    /// <see cref="Save(MutableDocument, ConcurrencyControl)" /> with a second argument of
    /// <see cref="ConcurrencyControl.LastWriteWins"/>
    /// </summary>
    /// <param name="document">The document to save</param>
    /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a collection
    /// other than the one it was previously added to</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public void Save(MutableDocument document) => Save(document, ConcurrencyControl.LastWriteWins);

    /// <summary>
    /// Saves the given <see cref="MutableDocument"/> into this collection
    /// </summary>
    /// <param name="document">The document to save</param>
    /// <param name="concurrencyControl">The rule to use when encountering a conflict in the collection</param>
    /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a collection
    /// other than the one it was previously added to</exception>
    /// <returns><c>true</c> if the save succeeded, <c>false</c> if there was a conflict</returns>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public bool Save(MutableDocument document, ConcurrencyControl concurrencyControl)
    {
        var doc = CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(document), document);
        return Save(doc, null, concurrencyControl, false);
    }

    /// <summary>
    /// Saves a document to the collection. When write operations are executed concurrently, 
    /// and if conflicts occur, conflict handler will be called. Use the handler to directly
    /// edit the document.Returning true, will save the document. Returning false, will cancel
    /// the save operation.
    /// </summary>
    /// <param name="document">The document to save</param>
    /// <param name="conflictHandler">The conflict handler block which can be used to resolve it.</param> 
    /// <returns><c>true</c> if the save succeeded, <c>false</c> if there was a conflict</returns>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public bool Save(MutableDocument document, Func<MutableDocument, Document?, bool> conflictHandler)
    {
        var doc = CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(document), document);
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(conflictHandler), conflictHandler);
        Document? baseDoc = null;
        bool saved;
        do {
            saved = Save(doc, baseDoc, ConcurrencyControl.FailOnConflict, false);
            baseDoc = new Document(this, doc.Id);
            if (!baseDoc.Exists) {
                throw new CouchbaseLiteException(C4ErrorCode.NotFound);
            }
            
            if (saved) {
                continue;
            }
            
            try {
                if (!conflictHandler(doc, baseDoc.IsDeleted ? null : baseDoc)) { // resolve conflict with conflictHandler
                    return false;
                }
            } catch {
                return false;
            }
        } while (!saved);// has conflict, save failed
        return saved;
    }

    /// <summary>
    /// Returns the expiration time of the document. <c>null</c> will be returned
    /// if there is no expiration time set
    /// </summary>
    /// <param name="docId"> The ID of the <see cref="Document"/> </param>
    /// <returns>Nullable expiration timestamp as a <see cref="DateTimeOffset"/> 
    /// of the document or <c>null</c> if time not set. </returns>
    /// <exception cref="CouchbaseLiteException">Throws NOT FOUND error if the document 
    /// doesn't exist</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public DateTimeOffset? GetDocumentExpiration(string docId)
    {
        CheckCollectionValid();
        using var doc = LiteCoreBridge.CheckTyped(err => NativeSafe.c4coll_getDoc(C4Coll, docId, true, C4DocContentLevel.DocGetCurrentRev, err));
        if (doc == null) {
            throw new CouchbaseLiteException(C4ErrorCode.NotFound);
        }

        C4Error err2 = new C4Error();
        var res = NativeSafe.c4coll_getDocExpiration(C4Coll, docId, &err2);
        if (res == 0) {
            if (err2.code == 0) {
                return null;
            }

            throw CouchbaseException.Create(err2);
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(res);
    }

    /// <summary>
    /// Sets an expiration date on a document. After this time, the document
    /// will be purged from the collection.
    /// </summary>
    /// <param name="docId"> The ID of the <see cref="Document"/> </param> 
    /// <param name="expiration"> Nullable expiration timestamp as a 
    /// <see cref="DateTimeOffset"/>, set timestamp to <c>null</c> 
    /// to remove expiration date time from doc.</param>
    /// <returns>Whether successfully sets an expiration date on the document</returns>
    /// <exception cref="CouchbaseLiteException">Throws NOT FOUND error if the document 
    /// doesn't exist</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public bool SetDocumentExpiration(string docId, DateTimeOffset? expiration)
    {
        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();
        return LiteCoreBridge.Check(err =>
        {
            if (expiration == null) {
                return NativeSafe.c4coll_setDocExpiration(C4Coll, docId, 0, err);
            }

            var millisSinceEpoch = expiration.Value.ToUnixTimeMilliseconds();
            return NativeSafe.c4coll_setDocExpiration(C4Coll, docId, millisSinceEpoch, err);
        });
    }

    /// <summary>
    /// Creates an index which could be a value index from <see cref="IndexBuilder.ValueIndex"/> or a full-text search index
    /// from <see cref="IndexBuilder.FullTextIndex"/> with the given name.
    /// The name can be used for deleting the index. Creating a new different index with an existing
    /// index name will replace the old index; creating the same index with the same name will be no-ops.
    /// </summary>
    /// <param name="name">The index name</param>
    /// <param name="index">The index</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> or <paramref name="index"/>
    /// is <c>null</c></exception>
    /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
    /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
    /// <exception cref="NotSupportedException">Thrown if an implementation of <see cref="IIndex"/> other than one of the library
    /// provided ones is used</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public void CreateIndex(string name, IIndex index)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(name), name);
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(index), index);

        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();
        var concreteIndex = Misc.TryCast<IIndex, QueryIndex>(index);
        var jsonObj = concreteIndex.ToJSON();
        var json = JsonSerializer.Serialize(jsonObj);
        LiteCoreBridge.Check(err =>
        {
            var internalOpts = concreteIndex.Options;

            // For some reason a "using" statement here causes a compiler error
            try {
                return NativeSafe.c4coll_createIndex(C4Coll, name, json, C4QueryLanguage.JSONQuery, concreteIndex.IndexType, &internalOpts, err);
            } finally {
                internalOpts.Dispose();
            }
        });
    }

    /// <summary>
    /// Gets a list of index names that are present in the collection
    /// </summary>
    /// <returns>The list of created index names</returns>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public IList<string> GetIndexes()
    {
        List<string> retVal = new List<string>();
        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();
        var result = new FLSliceResult();
        LiteCoreBridge.Check(err =>
        {
            result = NativeSafe.c4coll_getIndexesInfo(C4Coll, err);
            return result.buf != null;
        });

        var val = NativeRaw.FLValue_FromData(new FLSlice(result.buf, result.size), FLTrust.Trusted);
        if (val == null) {
            Native.FLSliceResult_Release(result);
            throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "FLValue_FromData failed...");
        }

        var indexesInfo = FLValueConverter.ToCouchbaseObject(val, Database, true) as IList<object> 
            ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "ToCouchbaseObject failed...");

        foreach (var a in indexesInfo) {
            var indexInfo = a as Dictionary<string, object>
                ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Corrupt entry in indexesInfo");
            retVal.Add(indexInfo["name"] as string ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Corrupt index data..."));
        }

        Native.FLSliceResult_Release(result);

        return retVal;
    }

    /// <summary>
    /// Creates an index with the given name, using one of the various specializations of <see cref="IndexConfiguration" />
    /// The name can be used for deleting the index. Creating a new different index with an existing
    /// index name will replace the old index; creating the same index with the same name will be no-ops.
    /// </summary>
    /// <param name="name">The index name</param>
    /// <param name="indexConfig">The index configuration</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> or <paramref name="indexConfig"/>
    /// is <c>null</c></exception>
    /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
    /// <exception cref="InvalidOperationException">Thrown if this method is called after the collection is closed</exception>
    /// <exception cref="NotSupportedException">Thrown if an implementation of <see cref="IIndex"/> other than one of the library
    /// provided ones is used</exception>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public void CreateIndex(string name, IndexConfiguration indexConfig)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(name), name);
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(indexConfig), indexConfig);

        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();
        indexConfig.Validate();
        LiteCoreBridge.Check(err =>
        {
            var internalOpts = indexConfig.Options;
            // For some reason a "using" statement here causes a compiler error
            try {
                return NativeSafe.c4coll_createIndex(C4Coll, name, indexConfig.ToSqlpp(), C4QueryLanguage.N1QLQuery, indexConfig.IndexType, &internalOpts, err);
            } finally {
                internalOpts.Dispose();
            }
        });
    }

    /// <summary>
    /// Deletes the index with the given name
    /// </summary>
    /// <param name="name">The name of the index to delete</param>
    /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotOpen"/>
    /// if this method is called after the collection is closed</exception>
    public void DeleteIndex(string name)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(name), name);

        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();
        LiteCoreBridge.Check(err => NativeSafe.c4coll_deleteIndex(C4Coll, name, err));
    }

    /// <summary>
    /// Create an <see cref="IQuery"/> object using the given SQL++ query
    /// expression.
    /// </summary>
    /// <param name="queryExpression">The SQL++ query expression (e.g. SELECT * FROM _)</param>
    /// <returns>THe initialized query object, ready to execute</returns>
    public IQuery CreateQuery(string queryExpression)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(queryExpression), queryExpression);
        CheckCollectionValid();

        var query = new NQuery(queryExpression, this.Database);
        return query;
    }

#if CBL_PLATFORM_APPLE
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4CollectionObserverCallback))]
#endif
    private static void DoDbObserverCallback(C4CollectionObserver* db, void* context)
    {
        var dbObj = GCHandle.FromIntPtr((IntPtr)context).Target as Collection;
        dbObj?._callbackFactory.StartNew(() =>
        {
            dbObj.PostDatabaseChanged();
        });
    }

#if CBL_PLATFORM_APPLE
        [ObjCRuntime.MonoPInvokeCallback(typeof(C4DocumentObserverCallback))]
#endif
    private static void DoDocObserverCallback(C4DocumentObserver* obs, C4Collection* collection, FLSlice docId, ulong sequence, void* context)
    {
        if (docId.buf == null) {
            return;
        }

        var dbObj = GCHandle.FromIntPtr((IntPtr)context).Target as Collection;
        dbObj?._callbackFactory.StartNew(() =>
        {
            dbObj.PostDocChanged(docId.CreateString()!);
        });
    }

    private void PostDocChanged(string documentID)
    {
        using var scope = ThreadSafety.BeginLockedScope();
        if (!IsClosed && _docObs.ContainsKey(documentID)) {
            var change = new DocumentChangedEventArgs(documentID, this);
            _documentChanged.Fire(documentID, this, change);
        }
    }

    private void FreeC4DbObserver()
    {
        if (_obs != null) {
            Native.c4dbobs_free(_obs);
            _obsContext.Free();
        }
    }

    private void ClearUnsavedDocsAndFreeDocObservers()
    {
        foreach (var obs in _docObs) {
            Native.c4docobs_free((C4DocumentObserver*)obs.Value.Item1);
            obs.Value.Item2.Free();
        }

        _docObs.Clear();
    }

    private Document? GetDocumentInternal(string docID)
    {
        CheckCollectionValid();
        var doc = new Document(this, docID);

        if (!doc.Exists || doc.IsDeleted) {
            doc.Dispose();
            WriteLog.To.Database.V(Tag, "Requested existing document {0}, but it doesn't exist",
                new SecureLogString(docID, LogMessageSensitivity.PotentiallyInsecure));
            return null;
        }

        return doc;
    }

    private void VerifyCollection(Document document)
    {
        if (document.Collection == null) {
            document.Collection = this;
        } else if (!document.Collection.Equals(this)) {
            throw new CouchbaseLiteException(C4ErrorCode.InvalidParameter,
                "The collection being used for Save does not match the one on the document being saved (" +
                "either it is a different collection or a collection from a different database instance).");
        }
    }

    private void PurgeDocById(string id)
    {
        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();
        LiteCoreBridge.Check(err => NativeSafe.c4coll_purgeDoc(C4Coll, id, err));
    }

    private bool Save(Document document, Document? baseDocument,
        ConcurrencyControl concurrencyControl, bool deletion)
    {
        if (deletion && document.RevisionID == null) {
            throw new CouchbaseLiteException(C4ErrorCode.NotFound,
                CouchbaseLiteErrorMessage.DeleteDocFailedNotSaved);
        }

        using var scope = ThreadSafety.BeginLockedScope();
        CheckCollectionValid();
        VerifyCollection(document);
        C4DocumentWrapper? curDoc = null;
        C4DocumentWrapper? newDoc = null;
        var committed = false;
        try {
            LiteCoreBridge.Check(err => NativeSafe.c4db_beginTransaction(C4Db, err));
            var baseDoc = baseDocument?.C4Doc;
            Save(document, ref newDoc, baseDoc, deletion);
            if (newDoc == null) {
                // Handle conflict:
                if (concurrencyControl == ConcurrencyControl.FailOnConflict) {
                    committed = true; // Weird, but if the next call fails I don't want to call it again in the catch block
                    LiteCoreBridge.Check(e => NativeSafe.c4db_endTransaction(C4Db, true, e));
                    return false;
                }

                C4Error err;
                curDoc = NativeSafe.c4coll_getDoc(C4Coll, document.Id, true, C4DocContentLevel.DocGetCurrentRev, &err);

                // If deletion and the current doc has already been deleted
                // or doesn't exist:
                if (deletion) {
                    if (curDoc == null) {
                        if (err.code == (int)C4ErrorCode.NotFound) {
                            return true;
                        }

                        throw CouchbaseException.Create(err);
                    } else if (curDoc.RawDoc->flags.HasFlag(C4DocumentFlags.DocDeleted)) {
                        document.ReplaceC4Doc(curDoc);
                        curDoc = null;
                        return true;

                    }
                }

                // Save changes on the current branch:
                if (curDoc == null) {
                    throw CouchbaseException.Create(err);
                }

                Save(document, ref newDoc, curDoc, deletion);
            }

            committed = true; // Weird, but if the next call fails I don't want to call it again in the catch block
            LiteCoreBridge.Check(e => NativeSafe.c4db_endTransaction(C4Db, true, e));
            document.ReplaceC4Doc(CBDebug.MustNotBeNull(WriteLog.To.Database, Tag, nameof(newDoc), newDoc));
            newDoc = null;
        } catch (Exception) {
            if (!committed) {
                LiteCoreBridge.Check(e => NativeSafe.c4db_endTransaction(C4Db, false, e));
            }

            throw;
        } finally {
            curDoc?.Dispose();
            newDoc?.Dispose();
        }

        return true;
    }

    private C4DocumentWrapper SaveFinal(Document doc, C4DocumentWrapper? baseDoc, FLSliceResult body, C4RevisionFlags revFlags)
    {
        var rawDoc = baseDoc ?? (doc.C4Doc?.HasValue == true ? doc.C4Doc : null);
        if (rawDoc != null) {
            return NativeHandler.Create()
                .AllowError((int)C4ErrorCode.Conflict, C4ErrorDomain.LiteCoreDomain).Execute(
                    err => NativeSafe.c4doc_update(rawDoc, (FLSlice)body, revFlags, err))!;
        }
        
        return NativeHandler.Create()
            .AllowError((int)C4ErrorCode.Conflict, C4ErrorDomain.LiteCoreDomain).Execute(
                err =>
                {
                    using var docID = new C4String(doc.Id);
                    return NativeSafe.c4coll_createDoc(C4Coll, docID.AsFLSlice(), (FLSlice)body, revFlags, err);
                })!;
    }

    private void Save(Document doc, ref C4DocumentWrapper? outDoc, C4DocumentWrapper? baseDoc, bool deletion)
    {
        var revFlags = (C4RevisionFlags)0;
        if (deletion) {
            revFlags = C4RevisionFlags.Deleted;
        }

        var body = (FLSliceResult)FLSlice.Null;
        if (!deletion && !doc.IsEmpty) {
            try {
                body = doc.Encode();
            } catch (ObjectDisposedException) {
                WriteLog.To.Database.E(Tag, "Save of disposed document {0} attempted, skipping...", new SecureLogString(doc.Id, LogMessageSensitivity.PotentiallyInsecure));
                return;
            }

            using var scope = ThreadSafety.BeginLockedScope();
            Debug.Assert(C4Db != null);
            FLDoc* fleeceDoc = Native.FLDoc_FromResultData(body,
                FLTrust.Trusted,
                NativeSafe.c4db_getFLSharedKeys(C4Db!), FLSlice.Null);
            if (NativeSafe.c4doc_dictContainsBlobs(C4Db!, (FLDict*)Native.FLDoc_GetRoot(fleeceDoc))) {
                revFlags |= C4RevisionFlags.HasAttachments;
            }

            Native.FLDoc_Release(fleeceDoc);
        } else if (doc.IsEmpty) {
            body = EmptyFLSliceResult();
        }

        try {
            outDoc = SaveFinal(doc, baseDoc, body, revFlags);
        } finally {
            Native.FLSliceResult_Release(body);
        }
    }

    internal bool IsIndexTrained(string name)
    {
        CheckCollectionValid();
        return LiteCoreBridge.Check(err =>
        {
            using var index = NativeSafe.c4coll_getIndex(_c4Coll, name, err);
            if (index != null) {
                return NativeSafe.c4index_isTrained(index, err);
            }
            
            WriteLog.To.Query.W(Tag, "Index {0} does not exist, returning false for IsIndexTrained", name);
            return false;
        });
    }

    /// <summary>
    /// Throws if this collection has been deleted, or its database closed.
    /// </summary>
    [MemberNotNull(nameof(C4Db), nameof(_c4Coll))]
    internal void CheckCollectionValid()
    {
        using var scope = ThreadSafety.BeginLockedScope();
        if (C4Db == null) {
            throw new CouchbaseLiteException(C4ErrorCode.NotOpen, CouchbaseLiteErrorMessage.DBClosedOrCollectionDeleted,
                new CouchbaseLiteException(C4ErrorCode.NotOpen, CouchbaseLiteErrorMessage.DBClosed));
        }

        if (_c4Coll == null || !NativeSafe.c4coll_isValid(_c4Coll)) {
            throw new CouchbaseLiteException(C4ErrorCode.NotOpen, CouchbaseLiteErrorMessage.DBClosedOrCollectionDeleted,
                new CouchbaseLiteException(C4ErrorCode.NotOpen, String.Format(CouchbaseLiteErrorMessage.CollectionNotAvailable, ToString())));
        }

    }

    private void PostDatabaseChanged()
    {
        using var scope = ThreadSafety.BeginLockedScope();
        if (_obs == null || IsClosed) {
            return;
        }

        const uint maxChanges = 100u;
        var external = false;
        uint nChanges;
        var changes = new C4CollectionChange[maxChanges];
        var docIDs = new List<string>();
        do {
            // Read changes in batches of MaxChanges:
            var collectionObservation = NativeSafe.c4dbobs_getChanges(_obs, changes, maxChanges);
            var newExternal = collectionObservation.external;
            nChanges = collectionObservation.numChanges;
            if (nChanges == 0 || external != newExternal || docIDs.Count > 1000) {
                if (docIDs.Count > 0) {
                    // Only notify if there are actually changes to send
                    var args = new CollectionChangedEventArgs(this, docIDs);
                    _databaseChanged.Fire(this, args);
                    docIDs = new List<string>();
                }
            }

            external = newExternal;
            for (var i = 0; i < nChanges; i++) {
                docIDs.Add(changes[i].docID.CreateString()!);
            }

            NativeSafe.c4dbobs_releaseChanges(changes, nChanges);
        } while (nChanges > 0);
    }

    private FLSliceResult EmptyFLSliceResult()
    {
        using var encoder = Database.SharedEncoder;
        encoder.BeginDict(0);
        encoder.EndDict();
        var body = encoder.Finish();
        encoder.Reset();

        return body;
    }

    private void Dispose(bool disposing)
    {
        if (IsClosed) {
            return;
        }

        if (disposing) {
            ClearUnsavedDocsAndFreeDocObservers();
            _c4Coll.Dispose();
            _c4Coll = null;
        }

        FreeC4DbObserver();
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, Scope);

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is not Collection other) {
            return false;
        }
            
        return IsValid == other.IsValid
            && String.Equals(Name, other.Name, StringComparison.Ordinal)
            && String.Equals(Scope.Name, other.Scope.Name, StringComparison.Ordinal)
            && ReferenceEquals(Database, other.Database);
    }

    /// <inheritdoc />
    public override string ToString() => $"COLLECTION[{Name}] of SCOPE[{Scope.Name}]";
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        using var scope = ThreadSafety.BeginLockedScope();
        if(IsClosed) {
            return;
        }

        Dispose(true);
    }
}
// 
//  Scope.cs
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

using Couchbase.Lite.Support;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    /// <summary>
    /// An object representing a Couchbase Lite Scope.  Scopes are a grouping level above 
    /// <see cref="Collection"/> objects that can segregate data.  There is not a direct
    /// SQL equivalent but it can be thought of a a logical grouping of tables with potential
    /// foreign key links.
    /// </summary>
    public sealed unsafe class Scope : IDisposable
    {
        #region Variales

        private ConcurrentDictionary<string, Collection> _collections = new ConcurrentDictionary<string, Collection>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the database that this scope belongs to
        /// </summary>
        public Database Database { get; internal set; }

        /// <summary>
        /// Gets the Scope Name
        /// </summary>
        /// <remarks>
        /// Naming rules:
        /// Must be between 1 and 251 characters in length.
        /// Can only contain the characters A-Z, a-z, 0-9, and the symbols _, -, and %. 
        /// Cannot start with _ or %.
        /// Case sensitive.
        /// </remarks>
        public string Name { get; internal set; } = Database._defaultScopeName;

        internal C4Database* c4Db
        {
            get {
                if (Database.c4db == null)
                    throw new CouchbaseLiteException(C4ErrorCode.NotOpen, CouchbaseLiteErrorMessage.DBClosedOrCollectionDeleted,
                        new CouchbaseLiteException(C4ErrorCode.NotOpen, CouchbaseLiteErrorMessage.DBClosed));
                return Database.c4db;
            }
        }

        internal ThreadSafety ThreadSafety { get; }

        /// <summary>
        /// Gets the total collections in the scope
        /// </summary>
        internal int Count
        {
            get {
                return _collections.Count;
            }
        }

        #endregion

        #region Constructors

        internal Scope(Database database)
        {
            Database = database;
            ThreadSafety = database.ThreadSafety;
        }

        internal Scope(Database database, string scope)
            :this(database)
        {
            Name = scope;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets one collection of the given name.  Note that this will cache after the first retrieval
        /// and return the same instance until said instance is disposed.  Be careful if using multiple 
        /// instances because disposing one will invalidate them all.
        /// </summary>
        /// <param name="name">The collection name</param>
        /// <returns>The collection of the given name. null if the collection doesn't exist in the Scope</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref = "CouchbaseLiteException" > Thrown with <see cref="C4ErrorCode.NotFound"/>
        /// if <see cref="Database"/> is closed</exception>
        /// <exception cref = "InvalidOperationException" > Thrown if <see cref="Collection"/> is not valid.</exception>
        public Collection? GetCollection(string name)
        {
            return ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                if (_collections.ContainsKey(name)) {
                    var c = _collections[name];
                    if (c.IsValid) {
                        return c;
                    } else {
                        // Remove invalid collection from cache
                        _collections.TryRemove(name, out var co);
                        co?.Dispose();
                    }
                }

                Collection? coll = null;
                var c4c = GetCollectionFromLiteCore(name);
                if (c4c != null) {
                    coll = new Collection(Database, name, this, c4c);
                    _collections.TryAdd(name, coll);
                }

                return coll == null || !coll.IsValid ? null : coll;
            });
        }

        /// <summary>
        /// Get all collections in this scope object.
        /// </summary>
        /// <returns>All collections in this scope object. Empty list if these is no collection in the Scope.</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public IReadOnlyList<Collection> GetCollections()
        {
            GetCollectionList();
            return _collections.Values as IReadOnlyList<Collection>
                ?? throw new CouchbaseLiteException(C4ErrorCode.UnexpectedError, "Invalid cast in GetCollections()");
        }

        #endregion

        #region Internal Methods

        internal Collection CreateCollection(string collectionName)
        {
            return ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                if (_collections.ContainsKey(collectionName)) {
                    var coll = _collections[collectionName];
                    if (coll.IsValid) {
                        return coll;
                    } else {
                        // Remove invalid collection from cache
                        _collections.TryRemove(collectionName, out var c);
                        c?.Dispose();
                    }
                }

                Collection co;
                using (var collName_ = new C4String(collectionName))
                using (var scopeName_ = new C4String(Name)) {
                    var collectionSpec = new C4CollectionSpec() 
                    {
                        name = collName_.AsFLSlice(),
                        scope = scopeName_.AsFLSlice()
                    };

                    var c4c = (C4Collection*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_createCollection(c4Db, collectionSpec, err);
                    });

                    // c4c is not null now, otherwise the above throws an exception
                    co = new Collection(Database, collectionName, this, c4c);
                    _collections.TryAdd(collectionName, co);
                }
                
                return co;
            });
        }

        internal bool DeleteCollection(string name, string scope)
        {
            bool deleteSuccessful = false;
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                using (var collName_ = new C4String(name))
                using (var scopeName_ = new C4String(scope)) {
                    var collectionSpec = new C4CollectionSpec()
                    {
                        name = collName_.AsFLSlice(),
                        scope = scopeName_.AsFLSlice()
                    };

                    deleteSuccessful = LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_deleteCollection(c4Db, collectionSpec, err);
                    });

                    if (deleteSuccessful) {
                        if (_collections.TryRemove(name, out var co)) {
                            co?.Dispose();
                        }
                    }
                }
            });

            return deleteSuccessful;
        }

        internal IReadOnlyList<Collection> GetCollectionListFromLiteCore()
        {
            List<Collection> cos = new List<Collection>();
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                C4Error error;
                var arrColl = Native.c4db_collectionNames(c4Db, Name, &error);
                if (error.code == 0) {
                    var collsCnt = Native.FLArray_Count((FLArray*)arrColl);
                    for (uint i = 0; i < collsCnt; i++) {
                        var collStr = (string?)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i));
                        var c4c = collStr != null ? GetCollectionFromLiteCore(collStr) : null;
                        if (c4c != null) {
                            // if c4c is not null, it is because collStr is not null
                            var coll = new Collection(Database, collStr!, this, c4c);
                            cos.Add(coll);
                        }
                    }
                }

                Native.FLValue_Release((FLValue*)arrColl);
            });

            return cos;
        }

        #endregion

        #region Private Methods

        private void CheckOpen()
        {
            if (c4Db == null) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.DBClosed);
            }
        }

        private void GetCollectionList()
        {
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                C4Error error;
                var arrColl = Native.c4db_collectionNames(c4Db, Name, &error);
                if (error.code == 0) {
                    var collsCnt = Native.FLArray_Count((FLArray*)arrColl);
                    if (_collections.Count > collsCnt)
                        _collections.Clear();

                    for (uint i = 0; i < collsCnt; i++) {
                        var collStr = (string?)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i));
                        if (collStr != null && !_collections.ContainsKey(collStr)) {
                            var c4c = GetCollectionFromLiteCore(collStr);
                            if (c4c != null) {
                                var coll = new Collection(Database, collStr, this, c4c);
                                _collections.TryAdd(collStr, coll);
                            }
                        }
                    }
                }

                Native.FLValue_Release((FLValue*)arrColl);
            });
        }

        private C4Collection* GetCollectionFromLiteCore(string collectionName)
        {
            C4Collection* co = null;
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                using (var collName_ = new C4String(collectionName))
                using (var scopeName_ = new C4String(Name)) {
                    var collectionSpec = new C4CollectionSpec() 
                    {
                        name = collName_.AsFLSlice(),
                        scope = scopeName_.AsFLSlice()
                    };

                    co = (C4Collection*) NativeHandler.Create().AllowError(new C4Error(C4ErrorCode.NotFound)).Execute(
                        err => Native.c4db_getCollection(c4Db, collectionSpec, err));
                }
            });

            return co;
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (!(obj is Scope other)) {
                return false;
            }

            return String.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override string ToString() => $"SCOPE[{Name}]";

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            ThreadSafety.DoLocked(() =>
            {
                if (_collections == null)
                    return;

                foreach (var c in _collections) {
                    c.Value.Dispose();
                }

                _collections.Clear();
            });
        }

        #endregion
    }
}

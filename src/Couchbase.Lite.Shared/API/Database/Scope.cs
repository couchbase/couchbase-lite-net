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
using JetBrains.Annotations;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Couchbase.Lite
{
    public sealed unsafe class Scope : IDisposable
    {
        #region Variales

        private ConcurrentDictionary<string, Collection> _collections = new ConcurrentDictionary<string, Collection>();

        #endregion

        #region Properties

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
                Debug.Assert(Database != null && Database.c4db != null);
                return Database.c4db;
            }
        }

        /// <summary>
        /// Gets the database that this document belongs to, if any
        /// </summary>
        [NotNull]
        internal Database Database { get; set; }

        [NotNull]
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

        internal Scope([NotNull] Database database)
        {
            Database = database;
            ThreadSafety = database.ThreadSafety;
        }

        internal Scope([NotNull] Database database, string scope)
            :this(database)
        {
            Name = scope;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets one collection of the given name
        /// </summary>
        /// <param name="name">The collection name</param>
        /// <returns>The collection of the given name. null if the collection doesn't exist in the Scope</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref = "CouchbaseLiteException" > Thrown with <see cref="C4ErrorCode.NotFound"/>
        /// if <see cref="Database"/> is closed</exception>
        /// <exception cref = "InvalidOperationException" > Thrown if <see cref="Collection"/> is not valid.</exception>
        public Collection GetCollection(string name)
        {
            Collection coll = null;
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                if (HasCollectionFromLiteCore(name)) {
                    if (!_collections.ContainsKey(name)) {
                        var c4c = GetCollectionFromLiteCore(name);
                        if (c4c != null) {
                            coll = new Collection(Database, name, this, c4c);
                            _collections.TryAdd(name, coll);
                        }
                    } else {
                        coll = _collections[name];
                    }
                }
            });

            coll?.CheckCollectionValid();
            return coll;
        }

        /// <summary>
        /// Get all collections in this scope object.
        /// </summary>
        /// <returns>All collections in this scope object. Empty list if these is no collection in the Scope.</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public IReadOnlyList<Collection> GetCollections()
        {
            GetCollectionList();
            return _collections?.Values as IReadOnlyList<Collection>;
        }

        #endregion

        #region Internal Methods

        internal Collection CreateCollection(string collectionName)
        {
            Collection co = null;
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                using (var collName_ = new C4String(collectionName))
                using (var scopeName_ = new C4String(Name)) {
                    var collectionSpec = new C4CollectionSpec() {
                        name = collName_.AsFLSlice(),
                        scope = scopeName_.AsFLSlice()
                    };

                    var c4c = (C4Collection*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_createCollection(c4Db, collectionSpec, err);
                    });

                    if (c4c != null) {
                        co = new Collection(Database, collectionName, this, c4c);
                        _collections.TryAdd(collectionName, co); 
                    }
                }
            });

            return co;
        }

        internal bool DeleteCollection(Collection collection)
        {
            bool deleteSuccessful = false;
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                using (var collName_ = new C4String(collection.Name))
                using (var scopeName_ = new C4String(collection.Scope?.Name)) {
                    var collectionSpec = new C4CollectionSpec() {
                        name = collName_.AsFLSlice(),
                        scope = scopeName_.AsFLSlice()
                    };

                    deleteSuccessful = LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_deleteCollection(c4Db, collectionSpec, err);
                    });

                    if (deleteSuccessful) {
                        Collection co = null;
                        if (_collections.TryRemove(collection.Name, out co)) {
                            co.Dispose();
                            co = null;
                        }
                    }
                }
            });

            return deleteSuccessful;
        }

        internal bool HasCollectionFromLiteCore(string collectionName)
        {
            bool hasCollection = false;
            ThreadSafety.DoLocked(() =>
            {
                CheckOpen();
                using (var collName_ = new C4String(collectionName))
                using (var scopeName_ = new C4String(Name)) {
                    var collectionSpec = new C4CollectionSpec() {
                        name = collName_.AsFLSlice(),
                        scope = scopeName_.AsFLSlice()
                    };

                    hasCollection = ThreadSafety.DoLocked(() =>
                    {
                        // Returns true if the collection exists.
                        return Native.c4db_hasCollection(c4Db, collectionSpec);
                    });
                }
            });

            return hasCollection;
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
                        var collStr = (string)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i));
                        var c4c = GetCollectionFromLiteCore(collStr);
                        if (c4c != null) {
                            var coll = new Collection(Database, collStr, this, c4c);
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
                        var collStr = (string)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i));
                        if (!_collections.ContainsKey(collStr)) {
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
                    var collectionSpec = new C4CollectionSpec() {
                        name = collName_.AsFLSlice(),
                        scope = scopeName_.AsFLSlice()
                    };

                    co = (C4Collection*)LiteCoreBridge.Check(err =>
                    {
                        return Native.c4db_getCollection(c4Db, collectionSpec, err);
                    });
                }
            });

            return co;
        }

        #endregion

        #region object
        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
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
                _collections = null;
            });
        }

        #endregion
    }
}

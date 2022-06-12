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
using System.Linq;
using System.Threading;

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

        /// <summary>
        /// Gets all collections in the Scope
        /// </summary>
        public IReadOnlyList<Collection> Collections => _collections.Values as IReadOnlyList<Collection>;

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
        /// <returns>null will be returned if the collection doesn't exist in the Scope</returns>
        public Collection GetCollection(string name)
        {
            Collection c = null;
            ThreadSafety.DoLocked(() =>
            {
                if (c4Db == null) {
                    throw new InvalidOperationException(CouchbaseLiteErrorMessage.DBClosed);
                }

                if (HasCollection(name)) {
                    c = _collections[name];
                }
            });

            return c;
        }

        /// <summary>
        /// Get all collections in this scope object.
        /// </summary>
        /// <returns>All collections in this scope object</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public IReadOnlyList<Collection> GetCollections()
        {
            GetCollectionList();
            return Collections;
        }

        #endregion

        #region Internal Methods

        internal Collection GetCollectionNotInCache(string collectionName)
        {
            Collection c = null;
            using (var collName_ = new C4String(collectionName))
            using (var scopeName_ = new C4String(Name)) {
                var collectionSpec = new C4CollectionSpec() {
                    name = collName_.AsFLSlice(),
                    scope = scopeName_.AsFLSlice()
                };

                var c4c = (C4Collection*)LiteCoreBridge.Check(err =>
                {
                    return Native.c4db_getCollection(c4Db, collectionSpec, err);
                });

                if (c4c != null) {
                    HasCollection(collectionName);
                }
            }

            return c;
        }

        internal Collection CreateCollection(string collectionName)
        {
            Collection c = null;
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
                    if (HasCollection(collectionName)) {
                        c = _collections[collectionName];
                    }
                }

                return c;
            }
        }

        internal bool DeleteCollection(Collection collection)
        {
            bool deleteSuccessful;
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
                    if(_collections.TryRemove(collection.Name, out var dummy)) {
                        collection.Dispose();
                        collection = null;
                    }
                }
            }

            return deleteSuccessful;
        }

        internal bool HasCollection(string collectionName)
        {
            bool hasCollection;
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

            if (hasCollection) {
                if (!_collections.ContainsKey(collectionName)) {
                    var c = new Collection(Database, collectionName, Name);
                    _collections.TryAdd(collectionName, c);
                }
            } else {
                if (_collections.ContainsKey(collectionName)) {
                    Collection c;
                    _collections.TryRemove(collectionName, out c);
                    c.Dispose();
                    c = null;
                }
            }

            return hasCollection;
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
                C4Error error;
                var arrColl = Native.c4db_collectionNames(c4Db, Name, &error);
                if (error.code == 0) {
                    var collsCnt = Native.FLArray_Count((FLArray*)arrColl);
                    if (_collections.Count > collsCnt)
                        _collections.Clear();

                    for (uint i = 0; i < collsCnt; i++) {
                        Collection c;
                        var collStr = (string)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i));
                        HasCollection(collStr);
                    }
                }

                Native.FLValue_Release((FLValue*)arrColl);
            });
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

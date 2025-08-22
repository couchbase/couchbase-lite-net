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
using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Lite
{
    /// <summary>
    /// An object representing a Couchbase Lite Scope.  Scopes are a grouping level above 
    /// <see cref="Collection"/> objects that can segregate data.  There is not a direct
    /// SQL equivalent but it can be thought of a a logical grouping of tables with potential
    /// foreign key links.
    /// </summary>
    public sealed unsafe class Scope
    {

        #region Properties

        /// <summary>
        /// Gets the database that this scope belongs to
        /// </summary>
        public Database Database { get; }

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
        public string Name { get; } = Database._defaultScopeName;

        internal C4DatabaseWrapper c4Db
        {
            get {
                if (Database.c4db == null)
                    throw new CouchbaseLiteException(C4ErrorCode.NotOpen, CouchbaseLiteErrorMessage.DBClosedOrCollectionDeleted,
                        new CouchbaseLiteException(C4ErrorCode.NotOpen, CouchbaseLiteErrorMessage.DBClosed));
                return Database.c4db;
            }
        }

        internal ThreadSafety ThreadSafety { get; }

        #endregion

        #region Constructors

        internal Scope(Database database, string scope = Database._defaultScopeName)
        {
            Database = database;
            ThreadSafety = database.ThreadSafety;
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
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            CheckOpen();

            Collection? coll = null;
            var c4c = GetCollectionFromLiteCore(name);
            if (c4c != null) {
                coll = new Collection(Database, name, this, c4c);
            }

            return coll is not { IsValid: true } ? null : coll;
        }

        /// <summary>
        /// Get all collections in this scope object.
        /// </summary>
        /// <returns>All collections in this scope object. Empty list if these is no collection in the Scope.</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public IReadOnlyList<Collection> GetCollections()
        {
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            CheckOpen();
            C4Error error;
            var arrColl = NativeSafe.c4db_collectionNames(c4Db, Name, &error);
            var collsCnt = Native.FLArray_Count((FLArray*)arrColl);
            var collections = new List<Collection>((int)collsCnt);
            if (error.code == 0) {
                for (uint i = 0; i < collsCnt; i++) {
                    var collStr = (string?)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i));
                    if (collStr != null) {
                        var c4c = GetCollectionFromLiteCore(collStr);
                        if (c4c != null) {
                            var coll = new Collection(Database, collStr, this, c4c);
                            collections.Add(coll);
                        }
                    }
                }
            }

            Native.FLValue_Release((FLValue*)arrColl);
            return collections;
        }

        #endregion

        #region Internal Methods

        internal Collection CreateCollection(string collectionName)
        {
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            CheckOpen();

            using var collName_ = new C4String(collectionName);
            using var scopeName_ = new C4String(Name);
            var collectionSpec = new C4CollectionSpec() 
            {
                name = collName_.AsFLSlice(),
                scope = scopeName_.AsFLSlice()
            };

            var c4c = LiteCoreBridge.CheckTyped(err => NativeSafe.c4db_createCollection(c4Db, collectionSpec, err))!;

            // c4c is not null now, otherwise the above throws an exception
            return new Collection(Database, collectionName, this, c4c);
        }

        internal bool DeleteCollection(string name, string scope)
        {
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            CheckOpen();
            using var collName_ = new C4String(name);
            using var scopeName_ = new C4String(scope);
            var collectionSpec = new C4CollectionSpec()
            {
                name = collName_.AsFLSlice(),
                scope = scopeName_.AsFLSlice()
            };

            return LiteCoreBridge.Check(err =>
                NativeSafe.c4db_deleteCollection(c4Db, collectionSpec, err));
        }

        #endregion

        #region Private Methods

        [MemberNotNull(nameof(c4Db))]
        private void CheckOpen()
        {
            if (c4Db == null) {
                throw new InvalidOperationException(CouchbaseLiteErrorMessage.DBClosed);
            }
        }

        private C4CollectionWrapper? GetCollectionFromLiteCore(string collectionName)
        {
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            CheckOpen();
            using var collName_ = new C4String(collectionName);
            using var scopeName_ = new C4String(Name);
            var collectionSpec = new C4CollectionSpec() 
            {
                name = collName_.AsFLSlice(),
                scope = scopeName_.AsFLSlice()
            };

            return NativeHandler.Create().AllowError(new C4Error(C4ErrorCode.NotFound)).Execute(
                err => NativeSafe.c4db_getCollection(c4Db, collectionSpec, err));
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override int GetHashCode() => Name.GetHashCode();

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (!(obj is Scope other)) {
                return false;
            }

            return String.Equals(Name, other.Name, StringComparison.Ordinal)
                && ReferenceEquals(Database, other.Database);
        }

        /// <inheritdoc />
        public override string ToString() => $"SCOPE[{Name}]";

        #endregion
    }
}

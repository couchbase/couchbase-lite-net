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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Lite;

/// <summary>
/// An object representing a Couchbase Lite Scope.  Scopes are a grouping level above 
/// <see cref="Collection"/> objects that can segregate data.  There is not a direct
/// SQL equivalent, but it can be thought of a logical grouping of tables with potential
/// foreign key links.
/// </summary>
public sealed unsafe class Scope
{

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
    /// Case-sensitive.
    /// </remarks>
    public string Name { get; }

    internal C4DatabaseWrapper C4Db
    {
        get {
            if (Database.C4db == null)
                throw new CouchbaseLiteException(C4ErrorCode.NotOpen, CouchbaseLiteErrorMessage.DBClosedOrCollectionDeleted,
                    new CouchbaseLiteException(C4ErrorCode.NotOpen, CouchbaseLiteErrorMessage.DBClosed));
            return Database.C4db;
        }
    }

    private ThreadSafety ThreadSafety { get; }

    internal Scope(Database database, string scope = Database.DefaultScopeName)
    {
        Database = database;
        ThreadSafety = database.ThreadSafety;
        Name = scope;
    }

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
        var c4Coll = GetCollectionFromLiteCore(name);
        if (c4Coll != null) {
            coll = new Collection(Database, name, this, c4Coll);
        }

        return coll is not { IsValid: true } ? null : coll;
    }

    /// <summary>
    /// Get all collections in this scope object.
    /// </summary>
    /// <returns>All collections in this scope object. Empty list if there are no collections in the Scope.</returns>
    /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
    public IReadOnlyList<Collection> GetCollections()
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        CheckOpen();
        C4Error error;
        var arrColl = NativeSafe.c4db_collectionNames(C4Db, Name, &error);
        var collCnt = Native.FLArray_Count((FLArray*)arrColl);
        var collections = new List<Collection>((int)collCnt);
        if (error.code == 0) {
            for (uint i = 0; i < collCnt; i++) {
                var collStr = (string?)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i));
                if (collStr == null) {
                    continue;
                }
                
                var c4Coll = GetCollectionFromLiteCore(collStr);
                if (c4Coll == null) {
                    continue;
                }
                
                var coll = new Collection(Database, collStr, this, c4Coll);
                collections.Add(coll);
            }
        }

        Native.FLValue_Release((FLValue*)arrColl);
        return collections;
    }

    internal Collection CreateCollection(string collectionName)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        CheckOpen();

        using var c4CollName = new C4String(collectionName);
        using var c4ScopeName = new C4String(Name);
        var collectionSpec = new C4CollectionSpec() 
        {
            name = c4CollName.AsFLSlice(),
            scope = c4ScopeName.AsFLSlice()
        };

        var c4Coll = LiteCoreBridge.CheckTyped(err => NativeSafe.c4db_createCollection(C4Db, collectionSpec, err))!;

        // c4c is not null now, otherwise the above throws an exception
        return new Collection(Database, collectionName, this, c4Coll);
    }

    internal bool DeleteCollection(string name, string scope)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        CheckOpen();
        using var c4CollName = new C4String(name);
        using var c4ScopeName = new C4String(scope);
        var collectionSpec = new C4CollectionSpec()
        {
            name = c4CollName.AsFLSlice(),
            scope = c4ScopeName.AsFLSlice()
        };

        return LiteCoreBridge.Check(err =>
            NativeSafe.c4db_deleteCollection(C4Db, collectionSpec, err));
    }

    [MemberNotNull(nameof(C4Db))]
    private void CheckOpen()
    {
        if (C4Db == null) {
            throw new InvalidOperationException(CouchbaseLiteErrorMessage.DBClosed);
        }
    }

    private C4CollectionWrapper? GetCollectionFromLiteCore(string collectionName)
    {
        using var threadSafetyScope = ThreadSafety.BeginLockedScope();
        CheckOpen();
        using var c4CollName = new C4String(collectionName);
        using var c4ScopeName = new C4String(Name);
        var collectionSpec = new C4CollectionSpec() 
        {
            name = c4CollName.AsFLSlice(),
            scope = c4ScopeName.AsFLSlice()
        };

        return NativeHandler.Create().AllowError(new C4Error(C4ErrorCode.NotFound)).Execute(
            err => NativeSafe.c4db_getCollection(C4Db, collectionSpec, err));
    }

    /// <inheritdoc />
    public override int GetHashCode() => Name.GetHashCode();

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is not Scope other) {
            return false;
        }

        return String.Equals(Name, other.Name, StringComparison.Ordinal)
            && ReferenceEquals(Database, other.Database);
    }

    /// <inheritdoc />
    public override string ToString() => $"SCOPE[{Name}]";
}
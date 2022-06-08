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

using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using JetBrains.Annotations;
using LiteCore.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public sealed unsafe class Scope : IDisposable
    {
        #region Variales

        #endregion

        List<Collection> _collections = new List<Collection>();

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
        public string Name { get; }

        /// <summary>
        /// Gets all collections in the Scope
        /// </summary>
        public IReadOnlyList<Collection> Collections
        {
            get {
                return _collections;
            }
        }

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

        internal Scope([NotNull] Database database, string scope)
        {
            Database = database;
            ThreadSafety = database.ThreadSafety;

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
            var colls = Collections as List<Collection>;
            return colls.SingleOrDefault(x => x.Name == name);
        }

        /// <summary>
        /// Get all collections in this scope object.
        /// </summary>
        /// <returns>All collections in this scope object</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        public IReadOnlyList<Collection> GetCollections()
        {
            ThreadSafety.DoLocked(() =>
            {
                if (c4Db == null) {
                    (Collections as IList<Collection>).Clear();
                    throw new InvalidOperationException(CouchbaseLiteErrorMessage.DBClosed);
                }

                var arrColl = Native.c4db_collectionNames(c4Db, Name);
                for (uint i = 0; i < Native.FLArray_Count((FLArray*)arrColl); i++) {
                    var collStr = (string)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i));
                    if (GetCollection(collStr) == null) {
                        var col = new Collection(Database, collStr, Name);
                        col.CreateCollection();
                        (Collections as IList<Collection>).Add(col);
                    }
                }

                Native.FLValue_Release((FLValue*)arrColl);
            });

            return Collections;
        }

        #endregion

        #region Internal Methods

        internal bool Add(Collection collection)
        {
            var res = collection.CreateCollection();
            if(res)
                _collections.Add(collection);

            return res;
        }

        internal bool Delete(Collection collection)
        {
            var res = collection.DeleteCollection();
            if (res)
                _collections.Remove(collection);

            return res;
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
                (Collections as List<Collection>).Clear();
                _collections = null;
            });
        }

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

        }

        #endregion
    }
}

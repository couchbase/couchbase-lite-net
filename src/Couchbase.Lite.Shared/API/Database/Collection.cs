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

using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using JetBrains.Annotations;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public sealed unsafe class Collection : IChangeObservable<CollectionChangedEventArgs>, IDocumentChangeObservable,
        IDisposable
    {
        #region Constants

        private const string Tag = nameof(Collection);

        public static readonly string DefaultScopeName = Database._defaultScopeName;
        public static readonly string DefaultCollectionName = Database._defaultCollectionName;

        #endregion

        #region Variables

        private C4Collection* _c4coll;

        #endregion

        #region Properties

        internal C4Database* c4Db
        {
            get {
                Debug.Assert(Database != null && Database.c4db != null);
                return Database.c4db;
            }
        }

        internal C4Collection* c4coll
        {
            get {
                C4Collection* retVal = null;
                ThreadSafety.DoLocked(() => retVal = _c4coll);
                return retVal;
            }
        }

        /// <summary>
        /// Gets the database that this collection belongs to, if any
        /// </summary>
        [NotNull]
        internal Database Database { get; set; }

        [NotNull]
        internal ThreadSafety ThreadSafety { get; }

        /// <summary>
        /// Gets the Collection Name
        /// </summary>
        /// <remarks>
        /// Naming rules:
        /// Must be between 1 and 251 characters in length.
        /// Can only contain the characters A-Z, a-z, 0-9, and the symbols _, -, and %. 
        /// Cannot start with _ or %.
        /// Case sensitive.
        /// </remarks>
        public string Name { get; private set; } = DefaultCollectionName;

        /// <summary>
        /// Gets the Scope of the Collection belongs to
        /// </summary>
        public Scope Scope { get; private set; }

        /// <summary>
        /// Gets the total documents in the Collection
        /// </summary>
        public ulong Count => ThreadSafety.DoLocked(() => Native.c4coll_getDocumentCount(c4coll));

        #endregion

        #region Constructors

        internal Collection([NotNull] Database database, string name, string scope, C4Collection* c4c)
        {
            Database = database;
            ThreadSafety = database.ThreadSafety;

            Name = name;
            Scope = database.GetScope(scope);

            _c4coll = c4c;
            Native.c4coll_retain(_c4coll);
        }

        #endregion

        #region IChangeObservable

        public ListenerToken AddChangeListener([CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<CollectionChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public ListenerToken AddChangeListener([NotNull] EventHandler<CollectionChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDocumentChangeObservable

        public ListenerToken AddDocumentChangeListener([NotNull] string id, [CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<DocumentChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public ListenerToken AddDocumentChangeListener([NotNull] string id, [NotNull] EventHandler<DocumentChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IChangeObservableRemovable

        public void RemoveChangeListener(ListenerToken token)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Deletes a document from the database.  When write operations are executed
        /// concurrently, the last writer will overwrite all other written values.
        /// Calling this method is the same as calling <see cref="Delete(Document, ConcurrencyControl)"/>
        /// with <see cref="ConcurrencyControl.LastWriteWins"/>
        /// </summary>
        /// <param name="document">The document</param>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.InvalidParameter"/>
        /// when trying to save a document into a database other than the one it was previously added to</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotFound"/>
        /// when trying to delete a document that hasn't been saved into a <see cref="Database"/> yet</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public void Delete([NotNull] Document document)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deletes the given <see cref="Document"/> from this database
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="concurrencyControl">The rule to use when encountering a conflict in the database</param>
        /// <returns><c>true</c> if the delete succeeded, <c>false</c> if there was a conflict</returns>
        /// <exception cref="CouchbaseException">Thrown if an error condition is returned from LiteCore</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.InvalidParameter"/>
        /// when trying to save a document into a database other than the one it was previously added to</exception>
        /// <exception cref="CouchbaseLiteException">Thrown with <see cref="C4ErrorCode.NotFound"/>
        /// when trying to delete a document that hasn't been saved into a <see cref="Database"/> yet</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called after the database is closed</exception>
        public bool Delete([NotNull] Document document, ConcurrencyControl concurrencyControl)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the <see cref="Document"/> with the specified ID
        /// </summary>
        /// <param name="id">The ID to use when creating or getting the document</param>
        /// <returns>The instantiated document, or <c>null</c> if it does not exist</returns>
        [CanBeNull]
        public Document GetDocument([NotNull] string id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Purges the given <see cref="Document"/> from the database.  This leaves
        /// no trace behind and will not be replicated
        /// </summary>
        /// <param name="document">The document to purge</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to purge a document from a database
        /// other than the one it was previously added to</exception>
        public void Purge([NotNull] Document document)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Purges the given document id of the <see cref="Document"/> 
        /// from the database.  This leaves no trace behind and will 
        /// not be replicated
        /// </summary>
        /// <param name="docId">The id of the document to purge</param>
        /// <exception cref="C4ErrorCode.NotFound">Throws NOT FOUND error if the document 
        /// of the docId doesn't exist.</exception>
        public void Purge([NotNull] string docId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Saves the given <see cref="MutableDocument"/> into this database.  This call is equivalent to calling
        /// <see cref="Save(MutableDocument, ConcurrencyControl)" /> with a second argument of
        /// <see cref="ConcurrencyControl.LastWriteWins"/>
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a database
        /// other than the one it was previously added to</exception>
        public void Save([NotNull] MutableDocument document)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Saves the given <see cref="MutableDocument"/> into this database
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="concurrencyControl">The rule to use when encountering a conflict in the database</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to save a document into a database
        /// other than the one it was previously added to</exception>
        /// <returns><c>true</c> if the save succeeded, <c>false</c> if there was a conflict</returns>
        public bool Save([NotNull] MutableDocument document, ConcurrencyControl concurrencyControl)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Saves a document to the database. When write operations are executed concurrently, 
        /// and if conflicts occur, conflict handler will be called. Use the handler to directly
        /// edit the document.Returning true, will save the document. Returning false, will cancel
        /// the save operation.
        /// </summary>
        /// <param name="document">The document to save</param>
        /// <param name="conflictHandler">The conflict handler block which can be used to resolve it.</param> 
        /// <returns><c>true</c> if the save succeeded, <c>false</c> if there was a conflict</returns>
        public bool Save(MutableDocument document, Func<MutableDocument, Document, bool> conflictHandler)
        {
            throw new NotImplementedException();
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
        public DateTimeOffset? GetDocumentExpiration(string docId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets an expiration date on a document. After this time, the document
        /// will be purged from the database.
        /// </summary>
        /// <param name="docId"> The ID of the <see cref="Document"/> </param> 
        /// <param name="expiration"> Nullable expiration timestamp as a 
        /// <see cref="DateTimeOffset"/>, set timestamp to <c>null</c> 
        /// to remove expiration date time from doc.</param>
        /// <returns>Whether successfully sets an expiration date on the document</returns>
        /// <exception cref="CouchbaseLiteException">Throws NOT FOUND error if the document 
        /// doesn't exist</exception>
        public bool SetDocumentExpiration(string docId, DateTimeOffset? expiration)
        {
            throw new NotImplementedException();
        }

        #endregion

        # region Public Methods - Indexable

        /// <inheritdoc />
        public IList<string> GetIndexes()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void CreateIndex([NotNull] string name, [NotNull] IndexConfiguration indexConfig)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void DeleteIndex([NotNull] string name)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Public Methods - QueryFactory

        public IQuery CreateQuery([NotNull] string queryExpression)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Internal Methods

        internal void GetSpec()
        {
            ThreadSafety.DoLocked(() =>
            {
                if (c4coll == null)
                    return;

                var spec = Native.c4coll_getSpec(c4coll);
                Name = spec.name.CreateString();
                Scope.Name = spec.scope.CreateString();
            });
        }

        /// <summary>
        /// Returns false if this collection has been deleted, or its database closed.
        /// </summary>
        internal bool IsCollectionValid()
        {
            bool isValid = false;
            ThreadSafety.DoLocked(() =>
            {
                isValid = Native.c4coll_isValid(_c4coll);
                if (!isValid) {
                    isValid = Native.c4coll_isValid(_c4coll);
                }
            });

            return isValid;
        }

        #endregion

        #region Private Methods

        private C4Database* GetC4Database()
        {
            C4Database* c4db = null;
            ThreadSafety.DoLocked(() =>
            {
                if (c4coll == null)
                    return;

                c4db = Native.c4coll_getDatabase(c4coll);
            });

            return c4db;
        }

        private void ReleaseCollection()
        {
            Native.c4coll_release(_c4coll);
            _c4coll = null;
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
            if (!(obj is Collection other)) {
                return false;
            }

            return String.Equals(Name, other.Name, StringComparison.Ordinal)
                && String.Equals(Scope.Name, other.Scope.Name, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override string ToString() => $"COLLECTION[{Name}] of SCOPE[{Scope.Name}]";
        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            ThreadSafety.DoLocked(() =>
            {
                ReleaseCollection();
            });
        }

        #endregion
    }
}

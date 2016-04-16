//
//  ICouchStore.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using Couchbase.Lite.Db;
using Couchbase.Lite.Revisions;

namespace Couchbase.Lite.Store
{
    internal delegate Status StoreValidation(RevisionInternal rev, RevisionInternal prevRev, RevisionID parentRevId);

    internal interface ICouchStore
    {
        #region Initialization and Configuration 

        /// <summary>
        /// Preflight to see if a database file exists in this directory. Called _before_ Open()!
        /// </summary>
        /// <returns><c>true</c>, if a database exists in the directory, <c>false</c> otherwise.</returns>
        /// <param name="directory">The directory to check</param>
        bool DatabaseExistsIn(string directory);

        /// <summary>
        /// Opens storage. Files will be created in the directory, which must already exist.
        /// </summary>
        /// <param name="directory">The existing directory to put data files into. The implementation may
        /// create as many files as it wants here. There will be a subdirectory called "attachments"
        /// which contains attachments; don't mess with that..</param>
        /// <param name="manager">The owning Manager; this is provided so the storage can examine its
        ///properties.</param>
        /// <param name="readOnly">Whether or not the database is read-only</param> 
        void Open(string directory, Manager manager, bool readOnly);

        /// <summary>
        /// Closes storage before it's deallocated. 
        /// </summary>
        void Close();

        /// <summary>
        /// The delegate object, which in practice is the Database.
        /// </summary>
        ICouchStoreDelegate Delegate { get; set; }

        /// <summary>
        /// The maximum depth a document's revision tree should grow to; beyond that, it should be pruned.
        /// This will be set soon after the Open() call.
        /// </summary>
        int MaxRevTreeDepth { get; set; }

        /// <summary>
        /// Whether the database storage should automatically (periodically) be compacted.
        /// This will be set soon after the Open() call.
        /// </summary>
        bool AutoCompact { get; set; }

        bool IsOpen { get; }

        IDatabaseUpgrader CreateUpgrader(Database upgradeTo, string upgradeFrom);

        #endregion

        #region Database Attributes & Operations

        /// <summary>
        /// Stores an arbitrary string under an arbitrary key, persistently.
        /// </summary>
        void SetInfo(string key, string info);

        /// <summary>
        /// Returns the value assigned to the given key by SetInfo().
        /// </summary>
        string GetInfo(string key);

        /// <summary>
        /// The number of (undeleted) documents in the database.
        /// </summary>
        int DocumentCount { get; }

        /// <summary>
        /// The last sequence number allocated to a revision.
        /// </summary>
        long LastSequence { get; }

        /// <summary>
        /// Is a transaction active?
        /// </summary>
        bool InTransaction { get; }

        /// <summary>
        /// Explicitly compacts document storage.
        /// </summary>
        void Compact();

        /// <summary>
        /// Executes the block within a database transaction.
        /// If the block returns a non-OK status, the transaction is aborted/rolled back.
        /// If the block returns DbBusy, the block will also be retried after a short delay;
        /// if 10 retries all fail, the DbBusy will be returned to the caller.
        /// Any exception raised by the block will be caught and treated as Exception.
        /// </summary>
        bool RunInTransaction(RunInTransactionDelegate block);

        /// <summary>
        /// Registers the encryption key of the database file. Must be called before opening the db.
        /// </summary>
        void SetEncryptionKey(SymmetricKey key);

        /// <summary>
        /// Called when the delegate changes its encryptionKey property. The storage should rewrite its
        /// files using the new key (which may be nil, meaning no encryption.)
        /// </summary>
        /// <returns>The action used to change the encryption key.</returns>
        /// <param name="newKey">The new key to use</param>
        AtomicAction ActionToChangeEncryptionKey(SymmetricKey newKey);

        #endregion

        #region Documents

        /// <summary>
        /// Retrieves a document revision by ID.
        /// </summary>
        /// <returns>The revision, or null if not found.</returns>
        /// <param name="docId">The document ID</param>
        /// <param name="revId">The revision ID; may be nil, meaning "the current revision".</param>
        /// <param name="withBody">Whether or not to include the document body</param>
        /// <param name="outStatus">Stores the reason that the returned value is null</param> 
        RevisionInternal GetDocument(string docId, RevisionID revId, bool withBody, Status outStatus = null);

        /// <summary>
        /// Loads the body of a revision.
        /// On entry, rev.docID and rev.revID will be valid.
        /// On success, rev.body will be valid.
        /// </summary>
        void LoadRevisionBody(RevisionInternal rev);

        /// <summary>
        /// Looks up the sequence number of a revision.
        /// Will only be called on revisions whose .sequence property is not already set.
        /// Does not need to set the revision's .sequence property; the caller will take care of that.
        /// </summary>
        long GetRevisionSequence(RevisionInternal rev);

        void SetDocumentExpiration(string documentId, DateTime? expiration);

        /// <summary>
        /// Retrieves the parent revision of a revision, or returns null if there is no parent.
        /// </summary>
        RevisionInternal GetParentRevision(RevisionInternal rev);

        /// <summary>
        /// Returns the given revision's list of direct ancestors (as Revision objects) in _reverse_
        /// chronological order, starting with the revision itself.
        /// </summary>
        IList<RevisionID> GetRevisionHistory(RevisionInternal rev, ICollection<RevisionID> ancestorRevIds);

        /// <summary>
        /// Returns all the known revisions (or all current/conflicting revisions) of a document.
        /// </summary>
        /// <returns>An array of all available revisions of the document.</returns>
        /// <param name="docId">The document ID</param>
        /// <param name="onlyCurrent">If <c>true</c>, only leaf revisions (whether or not deleted) should be returned.</param>
        RevisionList GetAllDocumentRevisions(string docId, bool onlyCurrent);

        /// <summary>
        /// Returns IDs of local revisions of the same document, that have a lower generation number.
        /// Does not return revisions whose bodies have been compacted away, or deletion markers.
        /// If 'onlyAttachments' is true, only revisions with attachments will be returned.
        /// </summary>
        IEnumerable<string> GetPossibleAncestors(RevisionInternal rev, int limit, bool onlyAttachments);

        /// <summary>
        /// Returns the most recent member of revIDs that appears in rev's ancestry.
        /// In other words: Look at the revID properties of rev, its parent, grandparent, etc.
        /// As soon as you find a revID that's in the revIDs array, stop and return that revID.
        /// If no match is found, return null.
        /// </summary>
        RevisionID FindCommonAncestor(RevisionInternal rev, IEnumerable<RevisionID> revIds);

        /// <summary>
        /// Looks for each given revision in the local database, and removes each one found from the list.
        /// On return, therefore, `revs` will contain only the revisions that don't exist locally.
        /// </summary>
        int FindMissingRevisions(RevisionList revs);

        /// <summary>
        /// Returns the keys (unique IDs) of all attachments referred to by existing un-compacted
        /// Each revision key is a BlobKey (raw SHA-1 digest) derived from the "digest" property 
        /// of the attachment's metadata.
        /// </summary>
        ICollection<BlobKey> FindAllAttachmentKeys();

        /// <summary>
        /// Iterates over all documents in the database, according to the given query options.
        /// </summary>
        IEnumerable<QueryRow> GetAllDocs(QueryOptions options);

        /// <summary>
        /// Returns all database changes with sequences greater than `lastSequence`.
        /// </summary>
        /// <returns>The since.</returns>
        /// <param name="lastSequence">The sequence number to start _after_</param>
        /// <param name="options">Options for ordering, document content, etc.</param>
        /// <param name="filter">If non-null, will be called on every revision, and those for which it returns <c>false</c>
        /// will be skipped.</param>
        RevisionList ChangesSince(long lastSequence, ChangesOptions options, RevisionFilter filter);

        #endregion

        #region Insertion / Deletion

        /// <summary>
        /// On success, before returning the new SavedRevision, the implementation will also call the
        /// Delegate's DatabaseStorageChanged() method to give it more details about the change.
        /// </summary>
        /// <returns>The new revision, with its revID and sequence filled in, or null on error.</returns>
        /// <param name="docId">The document ID, or nil if an ID should be generated at random.</param>
        /// <param name="prevRevId">The parent revision ID, or nil if creating a new document.</param>
        /// <param name="properties">The new revision's properties. (Metadata other than "_attachments" ignored.)</param>
        /// <param name="deleting"><c>true</c> if this revision is a deletion</param>
        /// <param name="allowConflict"><c>true</c> if this operation is allowed to create a conflict; otherwise a 409
        /// status will be returned if the parent revision is not a leaf.</param>
        /// <param name="validationBlock">If non-null, this block will be called before the revision is added.
        /// It's given the parent revision, with its properties if available, and can reject
        /// the operation by returning an error status.</param>
        RevisionInternal PutRevision(string docId, RevisionID prevRevId, IDictionary<string, object> properties,
            bool deleting, bool allowConflict, Uri source, StoreValidation validationBlock);

        /// <summary>
        /// Inserts an already-existing revision (with its revID), plus its ancestry, into a document.
        /// This is called by the pull replicator to add the revisions received from the server.
        /// On success, the implementation will also call the
        /// delegate's DatabaseStorageChanged() method to give it more details about the change.
        /// </summary>
        /// <param name="rev">The revision to insert. Its revID will be non-null.</param>
        /// <param name="revHistory">The revIDs of the revision and its ancestors, in reverse chronological order.
        /// The first item will be equal to inRev.revID.</param>
        /// <param name="validationBlock">If non-null, this block will be called before the revision is added.
        /// It's given the parent revision, with its properties if available, and can reject
        /// the operation by returning an error status.</param>
        /// <param name="source">The URL of the remote database this was pulled from, or null if it's local.
        /// (This will be used to create the DatabaseChange object sent to the delegate.</param>
        void ForceInsert(RevisionInternal rev, IList<RevisionID> revHistory, StoreValidation validationBlock, Uri source);

        /// <summary>
        /// Purges specific revisions, which deletes them completely from the local database 
        /// _without_ adding a "tombstone" revision. It's as though they were never there.
        /// </summary>
        /// <returns>On success will point to an IDictionary with the same form as docsToRev, 
        /// containing the doc/revision IDs that were actually removed.</returns>
        /// <param name="docsToRev">A dictionary mapping document IDs to arrays of revision IDs.
        /// The magic revision ID "*" means "all revisions", indicating that the
        /// document should be removed entirely from the database.</param>
        IDictionary<string, object> PurgeRevisions(IDictionary<string, IList<string>> docsToRev);

        IList<string> PurgeExpired();

        #endregion

        #region Views

        /// <summary>
        /// Instantiates storage for a view.
        /// </summary>
        /// <returns>Storage for the view, or null if create=<c>false</c> and it doesn't exist.</returns>
        /// <param name="name">The name of the view</param>
        /// <param name="create">If <c>true</c>, the view should be created; otherwise it must already exist</param>
        IViewStore GetViewStorage(string name, bool create);

        /// <summary>
        /// Returns the names of all existing views in the database.
        /// </summary>
        IEnumerable<string> GetAllViews();

        #endregion

        #region Local Docs

        /// <summary>
        /// Gets the local document.
        /// </summary>
        /// <returns>The local document.</returns>
        /// <param name="docId">Document identifier.</param>
        /// <param name="revId">Rev identifier.</param>
        RevisionInternal GetLocalDocument(string docId, RevisionID revId);

        /// <summary>
        /// Creates / updates / deletes a local document.
        /// </summary>
        /// <param name="revision">The new revision to save. Its docID must be set but the revID is ignored.
        /// If its .deleted property is <c>true</c>, it's a deletion.</param>
        /// <param name="prevRevId">The revision ID to replace</param>
        /// <param name="obeyMVCC">If <c>true</c>, the prevRevID must match the document's current revID (or nil if the
        /// document doesn't exist) or a 409 error is returned. If <c>false</c>, the prevRevID is
        /// ignored and the operation always succeeds.</param>
        RevisionInternal PutLocalRevision(RevisionInternal revision, RevisionID prevRevId, bool obeyMVCC);

        #endregion
    }
}

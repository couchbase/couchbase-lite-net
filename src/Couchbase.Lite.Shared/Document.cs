//
// Document.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

using Couchbase.Lite.Internal;
using Couchbase.Lite.Revisions;
using Couchbase.Lite.Util;
using System.Threading.Tasks;
using System.IO;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite {

    /// <summary>
    /// A Couchbase Lite Document.
    /// </summary>
Â?Â?Â?Â?public sealed class Document {

        private static readonly string Tag = typeof(Document).Name;
        SavedRevision currentRevision;
        private bool _currentRevisionKnown;
            
    #region Constructors

        /// <summary>Constructor</summary>
        /// <param name="database">The document's owning database</param>
        /// <param name="documentId">The document's ID</param>
        [Obsolete("Use Database CreateDocument or GetDocument")]
        public Document(Database database, string documentId)
        {
            _eventContext = database?.Manager?.CapturedContext ?? new TaskFactory(TaskScheduler.Current);
            Database = database;
            Id = documentId;
        }

        internal Document(Database database, string documentId, bool exists)
        {
            _eventContext = database?.Manager?.CapturedContext ?? new TaskFactory(TaskScheduler.Current);
            Database = database;
            Id = documentId;
            _currentRevisionKnown = !exists;
        }

    #endregion

        internal static bool IsValidDocumentId(string id)
        {
            // http://wiki.apache.org/couchdb/HTTP_Document_API#Documents
            if (String.IsNullOrEmpty (id)) {
                return false;
            }

            return id [0] != '_' || id.StartsWith ("_design/", StringComparison.InvariantCultureIgnoreCase);
        }
    
Â?Â?Â?Â?#region Instance Members

        private readonly TaskFactory _eventContext;

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/> that owns this <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Database"/> that owns this <see cref="Couchbase.Lite.Document"/>.</value>
        public Database Database { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Document"/>'s id.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Document"/>'s id.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets if the <see cref="Couchbase.Lite.Document"/> is deleted.
        /// </summary>
        /// <value><c>true</c> if deleted; otherwise, <c>false</c>.</value>
        public bool Deleted { get { return CurrentRevision == null && LeafRevisions.Any (); } }

        /// <summary>
        /// Gets if the <see cref="Couchbase.Lite.Document"/> is expired and should be auto-purged.
        /// </summary>
        /// <value><c>true</c> if expired; otherwise, <c>false</c>.</value>
        /*public bool Expired 
        { 
            get {
                var exp = Database.Storage.GetDocumentExpiration(Id);
                if (!exp.HasValue) {
                    return false;
                }

                var nowStamp = DateTime.Now.MillisecondsSinceEpoch() / 1000;
                return exp <= nowStamp;
            }
        }*/

        /// <summary>
        /// If known, gets the Id of the current <see cref="Couchbase.Lite.Revision"/>, otherwise null.
        /// </summary>
        /// <value>The Id of the current <see cref="Couchbase.Lite.Revision"/> if known, otherwise null.</value>
        public string CurrentRevisionId {
            get {
                var cr = CurrentRevision;
                return cr == null 
                    ? null
                    : cr.Id;
            }
        }

        /// <summary>
        /// Gets the current/latest <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <value>The current/latest <see cref="Couchbase.Lite.Revision"/>.</value>
        public SavedRevision CurrentRevision { 
            get {
                if(!_currentRevisionKnown) {
                    var status = new Status();
                    currentRevision = GetRevisionFromRev(Database.GetDocument(Id, null, true, status));
                    if(currentRevision != null || status.Code == StatusCode.NotFound || status.IsSuccessful) {
                        _currentRevisionKnown = true;
                    }
                }

                return currentRevision;
            }
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Document"/>'s <see cref="Couchbase.Lite.Revision"/> history 
        /// in forward order. Older, ancestor, <see cref="Couchbase.Lite.Revision"/>s are not guaranteed to 
        /// have their properties available.
        /// </summary>
        /// <value>
        /// The <see cref="Couchbase.Lite.Document"/>'s <see cref="Couchbase.Lite.Revision"/> history 
        /// in forward order.
        /// </value>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while getting the Revision history.
        /// </exception>
        public IEnumerable<SavedRevision> RevisionHistory {
            get {
                if (CurrentRevision == null) {
                    Log.To.Database.W(Tag, "RevisionHistory called but no CurrentRevision");
                    return null;
                }

                return CurrentRevision.RevisionHistory;
            }
        }

        /// <summary>
        /// Gets all of the current conflicting <see cref="Couchbase.Lite.Revision"/>s for the 
        /// <see cref="Couchbase.Lite.Document"/>. If the <see cref="Couchbase.Lite.Document"/> is not in conflict, 
        /// only the single current <see cref="Couchbase.Lite.Revision"/> will be returned.
        /// </summary>
        /// <value>
        /// All of the current conflicting <see cref="Couchbase.Lite.Revision"/>s for the 
        /// <see cref="Couchbase.Lite.Document"/>.
        /// </value>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while getting the conflicting <see cref="Couchbase.Lite.Revision"/>s.
        /// </exception>
        public IEnumerable<SavedRevision> ConflictingRevisions { get { return GetLeafRevisions(false); } }

        /// <summary>
        /// Gets all of the leaf <see cref="Couchbase.Lite.Revision"/>s in the <see cref="Couchbase.Lite.Document"/>'s 
        /// <see cref="Couchbase.Lite.Revision"/> tree.
        /// </summary>
        /// <value>
        /// All of the leaf <see cref="Couchbase.Lite.Revision"/>s in the <see cref="Couchbase.Lite.Document"/>'s 
        /// <see cref="Couchbase.Lite.Revision"/> tree.
        /// </value>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while getting the leaf <see cref="Couchbase.Lite.Revision"/>s.
        /// </exception>
        public IEnumerable<SavedRevision> LeafRevisions { get { return GetLeafRevisions(true); } }

        /// <summary>
        /// Gets the properties of the current <see cref="Couchbase.Lite.Revision"/> of 
        /// the <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <remarks>
        /// The contents of the current revision of the document.
        /// This is shorthand for self.currentRevision.properties.
        /// Any keys in the dictionary that begin with "_", such as "_id" and "_rev", 
        /// contain CouchbaseLite metadata.
        /// </remarks>
        /// <value>
        /// The properties of the current <see cref="Couchbase.Lite.Revision"/> of 
        /// the <see cref="Couchbase.Lite.Document"/>
        /// </value>
        public IDictionary<String, Object> Properties { get { return CurrentRevision != null ? CurrentRevision.Properties : null; } }

        /// <summary>
        /// Gets the properties of the current <see cref="Couchbase.Lite.Revision"/> of the 
        /// <see cref="Couchbase.Lite.Document"/> without any properties 
        /// with keys prefixed with '_' (which contain Couchbase Lite data).
        /// </summary>
        /// <remarks>
        /// The user-defined properties, without the ones reserved by CouchDB.
        /// This is based on -properties, with every key whose name starts with "_" removed.
        /// </remarks>
        /// <value>
        /// The properties of the current <see cref="Couchbase.Lite.Revision"/> of the 
        /// <see cref="Couchbase.Lite.Document"/> without any properties 
        /// with keys prefixed with '_'.
        /// </value>
        public IDictionary<String, Object> UserProperties { get { return CurrentRevision != null ? CurrentRevision.UserProperties : null; } }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <remarks>
        /// Deletes the <see cref="Couchbase.Lite.Document"/> by adding a deletion <see cref="Couchbase.Lite.Revision"/>.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while deleting the <see cref="Couchbase.Lite.Document"/>.
        /// </exception>
        public void Delete() { if (CurrentRevision != null) { CurrentRevision.DeleteDocument(); } }

        /// <summary>
        /// Completely purges the <see cref="Couchbase.Lite.Document"/> from the local <see cref="Couchbase.Lite.Database"/>. 
        /// This is different from delete in that it completely deletes everything related to the 
        /// <see cref="Couchbase.Lite.Document"/> and does not replicate the deletes to other <see cref="Couchbase.Lite.Database"/>s.
        /// </summary>
        /// <remarks>
        /// Purges this document from the database; this is more than deletion, it forgets entirely about it.
        /// The purge will NOT be replicated to other databases.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while purging the <see cref="Couchbase.Lite.Document"/>.
        /// </exception>
        public void Purge()
        {
            var revs = new List<String>();
            revs.Add("*");

            var docsToRevs = new Dictionary<String, IList<String>>();
            docsToRevs[Id] = revs;

            Database.Storage.PurgeRevisions(docsToRevs);
            Database.RemoveDocumentFromCache(this);
        }

        /// <summary>
        /// Returns the <see cref="Couchbase.Lite.Revision"/> with the specified id if it exists, otherwise null.
        /// </summary>
        /// <param name="id">The <see cref="Couchbase.Lite.Revision"/> id.</param>
        /// <returns>The <see cref="Couchbase.Lite.Revision"/> with the specified id if it exists, otherwise null</returns>
        public SavedRevision GetRevision(string id)
        {
            if (id == null) {
                return null;
            }

            if (CurrentRevision != null && id.Equals(CurrentRevision.Id))
                return CurrentRevision;

            var revisionInternal = Database.GetDocument(Id, id.AsRevID(), true);

            var revision = GetRevisionFromRev(revisionInternal);
            return revision;
        }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.UnsavedRevision"/> whose properties and attachments are initially 
        /// identical to the current <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        /// <remarks>
        /// Creates an unsaved new revision whose parent is the currentRevision,
        /// or which will be the first revision if the document doesn't exist yet.
        /// You can modify this revision's properties and attachments, then save it.
        /// No change is made to the database until/unless you save the new revision.
        /// </remarks>
        /// <returns>
        /// A new <see cref="Couchbase.Lite.UnsavedRevision"/> whose properties and attachments are initially 
        /// identical to the current <see cref="Couchbase.Lite.Revision"/>
        /// </returns>
        public UnsavedRevision CreateRevision()
        {
            return new UnsavedRevision(this, CurrentRevision);
        }

        /// <summary>
        /// Returns the value of the property with the specified key.
        /// </summary>
        /// <returns>The value of the property with the specified key.</returns>
        /// <param name="key">The key of the property value to return.</param>
        public Object GetProperty(String key) { return CurrentRevision != null ? CurrentRevision.Properties.Get(key) : null; }

        /// <summary>
        /// Returns the TValue of the property with the specified key.
        /// </summary>
        /// <returns>The value of the property with the specified key as TValue.</returns>
        /// <param name="key">The key of the property value to return.</param>
        public TValue GetProperty<TValue>(String key)
        {
            TValue val;
            try
            {
                val = (TValue)GetProperty(key);
            }
            catch (InvalidCastException)
            {
                val = default(TValue);
            }
            return val;
        }

        /// <summary>
        /// Creates and saves a new <see cref="Couchbase.Lite.Revision"/> with the specified properties. 
        /// To succeed the specified properties must include a '_rev' property whose value maches 
        /// the current <see cref="Couchbase.Lite.Revision"/>'s id.
        /// </summary>
        /// <remarks>
        /// Saves a new revision. The properties dictionary must have a "_rev" property
        /// whose ID matches the current revision's (as it will if it's a modified
        /// copy of this document's .properties property.)
        /// </remarks>
        /// <param name="properties">The properties to set on the new Revision.</param>
        /// <returns>The new <see cref="Couchbase.Lite.SavedRevision"/></returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an error occurs while creating or saving the new <see cref="Couchbase.Lite.Revision"/>.
        /// </exception>
        public SavedRevision PutProperties(IDictionary<string, object> properties)
        {
            var prevID = properties.CblRev();
            return PutProperties(properties, prevID, false);
        }

        /// <summary>
        /// Creates and saves a new <see cref="Couchbase.Lite.Revision"/> by allowing the caller to update 
        /// the existing properties. Conflicts are handled by calling the delegate again.
        /// </summary>
        /// <remarks>
        /// Saves a new revision by letting the caller update the existing properties.
        /// This method handles conflicts by retrying (calling the block again).
        /// The DocumentUpdater implementation should modify the properties of the new revision and return YES to save or
        /// NO to cancel. Be careful: the DocumentUpdater can be called multiple times if there is a conflict!
        /// </remarks>
        /// <param name="updateDelegate">
        /// The delegate that will be called to update the new <see cref="Couchbase.Lite.Revision"/>'s properties.
        /// return YES, or just return NO to cancel.
        /// </param>
        /// <returns>The new <see cref="Couchbase.Lite.SavedRevision"/>, or null on error or cancellation.</returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an error occurs while creating or saving the new <see cref="Couchbase.Lite.Revision"/>.
        /// </exception>
        public SavedRevision Update(UpdateDelegate updateDelegate)
        {
            Debug.Assert(updateDelegate != null);
            if (updateDelegate == null) {
                return null;
            }

            var lastErrorCode = StatusCode.Unknown;
            do
            {
                // Force the database to load the current revision
                // from disk, which will happen when CreateRevision
                // sees that currentRevision is null.
                if (lastErrorCode == StatusCode.Conflict)
                {
                    currentRevision = null;
                }

                using(UnsavedRevision newRev = CreateRevision()) {
                    if (!updateDelegate(newRev)) {
                        break;
                    }

                    try {
                        SavedRevision savedRev = newRev.Save();
                        if (savedRev != null) {
                            return savedRev;
                        }
                    } catch (CouchbaseLiteException e) {
                        lastErrorCode = e.CBLStatus.Code;
                    }
                }
            } while (lastErrorCode == StatusCode.Conflict);

            return null;
        }

        /// <summary>
        /// Adds an existing revision copied from another database.  Unlike a normal insertion, this does
        /// not assign a new revision ID; instead the revision's ID must be given.  Ths revision's history
        /// (ancestry) must be given, which can put it anywhere in the revision tree.  It's not an error if
        /// the revision already exists locally; it will just be ignored.
        /// 
        /// This is not an operation that clients normall perform; it's used by the replicator.  You might want
        /// to use it if you're pre-loading a database with canned content, or if you're implementing some new
        /// kind of replicator that transfers revisions from another database
        /// </summary>
        /// <param name="properties">The properties of the revision (_id and _rev will be ignored but _deleted
        /// and _attachments are recognized)</param>
        /// <param name="attachments">A dictionary providing attachment bodies.  The keys are the attachment
        /// names (matching the keys in the properties `_attachments` dictionary) and the values are streams
        /// that contain the attachment bodies.</param>
        /// <param name="revisionHistory">The revision history in the form of an array of revision-ID strings, in
        /// reverse chronological order.  The first item must be the new revision's ID.  Following items are its
        /// parent's ID, etc.</param>
        /// <param name="sourceUri">The URL of the database this revision came from, if any.  (This value
        /// shows up in the Database Changed event triggered by this insertion, and can help clients decide
        /// whether the change is local or not)</param>
        /// <returns><c>true</c> on success, false otherwise</returns>
        public bool PutExistingRevision(IDictionary<string, object> properties, IDictionary<string, Stream> attachments, IList<string> revisionHistory, Uri sourceUri)
        {
            if(revisionHistory == null || revisionHistory.Count == 0) {
                Log.To.Database.E(Tag, "Invalid revision history in PutExistingRevision (must contain at " +
                    "least one revision ID), throwing...");
                throw new ArgumentException("revisionHistory");
            }

            var revIDs = revisionHistory.AsRevIDs().ToList();
            var rev = new RevisionInternal(Id, revIDs[0], properties.CblDeleted());
            rev.SetProperties(PropertiesToInsert(properties));
            if(!Database.RegisterAttachmentBodies(attachments, rev)) {
                Log.To.Database.W(Tag, "Failed to register attachment bodies, aborting insert...");
                return false;
            }

            Database.ForceInsert(rev, revIDs, sourceUri);
            return true;
        }

        /// <summary>
        /// Sets an absolute point in time for the document to expire.  Must be
        /// a DateTime in the future.
        /// </summary>
        /// <param name="expireTime">The time at which the document expires, and is
        /// eligible to be auto-purged</param>
        /// <exception cref="System.InvalidOperationException">The expireTime is not in the future</exception>
        public void ExpireAt(DateTime expireTime)
        {
            var nowStamp = DateTime.UtcNow.MillisecondsSinceEpoch() / 1000;
            var stamp = expireTime.MillisecondsSinceEpoch() / 1000;
            if (stamp <= nowStamp) {
                throw new InvalidOperationException("ExpireAt must provide a date in the future");
            }

            Database.Storage.SetDocumentExpiration(Id, stamp);
        }

        /// <summary>
        /// Sets an interval to wait before expiring the document.
        /// </summary>
        /// <param name="timeInterval">The time to wait before expiring the document and
        /// making it eligible for auto-purging.</param>
        public void ExpireAfter(TimeSpan timeInterval)
        {
            var expireTime = DateTime.UtcNow + timeInterval;
            var stamp = expireTime.MillisecondsSinceEpoch() / 1000;
            Database.Storage.SetDocumentExpiration(Id, stamp);
        }

        /// <summary>
        /// Cancels the expiration date on the document
        /// </summary>
        public void CancelExpire()
        {
            Database.Storage.SetDocumentExpiration(Id, null);
        }

        /// <summary>
        /// Adds or Removed a change delegate that will be called whenever the Document changes
        /// </summary>
        public event EventHandler<DocumentChangeEventArgs> Change
        {
            add { _change = (EventHandler<DocumentChangeEventArgs>)Delegate.Combine(_change, value); }
            remove { _change = (EventHandler<DocumentChangeEventArgs>)Delegate.Remove(_change, value); }
        }
        private EventHandler<DocumentChangeEventArgs> _change;

Â?Â?Â?Â?#endregion


    #region Non-public Members

        private IDictionary<string, object> PropertiesToInsert(IDictionary<string, object> properties)
        {
            var idProp = properties.CblID();
            if(idProp != null && idProp != Id) {
                Log.To.Database.W(Tag, "Trying to PUT wrong _id to {0}: {1}", this, new SecureLogJsonString(properties, LogMessageSensitivity.PotentiallyInsecure));
            }

            var nuProperties = properties == null ? null : new Dictionary<string, object>(properties);

            // Process attachments dict, converting Attachments in dicts
            var attachments = properties.CblAttachments();
            if(attachments != null && attachments.Count > 0) {
                var expanded = Attachment.InstallAttachmentBodies(attachments, Database);
                if(expanded != attachments) {
                    nuProperties["_attachments"] = expanded;
                }
            }

            return nuProperties;
        }

        internal void ForgetCurrentRevision()
        {
            _currentRevisionKnown = false;
            currentRevision = null;
        }

        internal SavedRevision GetRevisionWithId(RevisionID revId)
        {
            return GetRevisionWithId(revId, true);
        }

        internal SavedRevision GetRevisionWithId(RevisionID revId, bool withBody)
        {
            if(revId == null) {
                return null;
            }

            if(revId.Equals(currentRevision.Id)) {
                return currentRevision;
            }

            return GetRevisionFromRev(Database.GetDocument(Id, revId, withBody));
        }

        internal void LoadCurrentRevisionFrom(QueryRow row)
        {
            if(row.DocRevID == null) {
                return;
            }

            var revId = row.DocRevID;
            if (currentRevision == null || revId.CompareTo(CurrentRevisionId.AsRevID()) > 0)
            {
                ForgetCurrentRevision();
                var rev = row.DocumentRevision;
                if(rev != null) {
                    currentRevision = new SavedRevision(this, rev);
                    _currentRevisionKnown = true;
                }
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>       
        internal SavedRevision PutProperties(IDictionary<string, object> properties, RevisionID prevID, bool allowConflict)
        {
            var newRev = Database.PutDocument(Id, PropertiesToInsert(properties), prevID, allowConflict, null);
            if(newRev == null) {
                return null;
            }

            return GetRevisionFromRev(newRev);
        }

        /// <summary>
        /// Returns all the leaf revisions in the document's revision tree,
        /// including deleted revisions (i.e.
        /// </summary>
        /// <remarks>
        /// Returns all the leaf revisions in the document's revision tree,
        /// including deleted revisions (i.e. previously-resolved conflicts.)
        /// </remarks>
        /// <returns>all the leaf revisions in the document's revision tree</returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal IList<SavedRevision> GetLeafRevisions(bool includeDeleted)
        {
            var result = new List<SavedRevision>();
            var revs = Database.Storage.GetAllDocumentRevisions(Id, true);
            foreach (RevisionInternal rev in revs)
            {
                // add it to result, unless we are not supposed to include deleted and it's deleted
                if (!includeDeleted && rev.Deleted)
                {
                    // don't add it
                }
                else
                {
                    result.Add(GetRevisionFromRev(rev));
                }
            }
            return new ReadOnlyCollection<SavedRevision>(result);
        }

        internal SavedRevision GetRevisionFromRev(RevisionInternal internalRevision)
        {
            if (internalRevision == null) {
                return null;
            }

            if (currentRevision != null && internalRevision.RevID.Equals(CurrentRevision.Id)) {
                return currentRevision;
            }
            else {
                return new SavedRevision(this, internalRevision);
            }
        }

        internal void RevisionAdded(DocumentChange documentChange, bool notify)
        {
            var revId = documentChange.WinningRevisionId;
            if (revId == null) {
                return; // current revision didn't change
            }

            if (_currentRevisionKnown && (currentRevision == null || !revId.Equals(currentRevision.Id)))
            {
                var rev = documentChange.WinningRevisionIfKnown;
                if (rev == null) {
                    ForgetCurrentRevision();
                } else if (rev.Deleted) {
                    currentRevision = null;
                } else {
                    currentRevision = new SavedRevision(this, rev);
                }
            }

            if (!notify) {
                return;
            }

            var args = new DocumentChangeEventArgs {
                Change = documentChange,
                Source = this
            } ;

            Log.To.TaskScheduling.V(Tag, "Scheduling Change callback...");
            _eventContext.StartNew(() =>
            {
                var changeEvent = _change;
                if (changeEvent != null) {
                    Log.To.TaskScheduling.V(Tag, "Firing Change callback...");
                    changeEvent(this, args);
                } else {
                    Log.To.TaskScheduling.V(Tag, "Change callback is null, not firing...");
                }
            });
        }

    #endregion
Â?Â?Â?Â?
Â?Â?Â?Â?#region Delegates

        /// <summary>
        /// A delegate that can be used to update a <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <param name="revision">
        /// The <see cref="Couchbase.Lite.UnsavedRevision"/> to update.
        /// </param>
        /// <returns>
        /// True if the <see cref="Couchbase.Lite.UnsavedRevision"/> should be saved, otherwise false.
        /// </returns>
        public delegate Boolean UpdateDelegate(UnsavedRevision revision);

Â?Â?Â?Â?#endregion
Â?Â?Â?Â?
Â?Â?Â?Â?#region EventArgs Subclasses
        /// <summary>
        /// The type of event raised when a <see cref="Couchbase.Lite.Document"/> changes. 
        /// This event is not raised in response to local <see cref="Couchbase.Lite.Document"/> changes.
        ///</summary>
        public class DocumentChangeEventArgs : EventArgs {

        Â?Â?Â?Â?//Properties
            /// <summary>
            /// Gets the <see cref="Couchbase.Lite.Document"/> that raised the event.
            /// </summary>
            /// <value>The <see cref="Couchbase.Lite.Document"/> that raised the event</value>
        Â?Â?Â?Â?public Document Source { get; internal set; }

            /// <summary>
            /// Gets the details of the change.
            /// </summary>
            /// <value>The details of the change.</value>
        Â?Â?Â?Â?public DocumentChange Change { get; internal set; }

        }

Â?Â?Â?Â?#endregion
Â?Â?Â?Â?
Â?Â?Â?Â?}


}


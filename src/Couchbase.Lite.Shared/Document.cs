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
using System.Diagnostics;
using System.Linq;
using System.Text;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using Sharpen;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite {

    /// <summary>
    /// A Couchbase Lite Document.
    /// </summary>
    public sealed class Document {

        SavedRevision currentRevision;

        #region Constructors

        /// <summary>Constructor</summary>
        /// <param name="database">The document's owning database</param>
        /// <param name="documentId">The document's ID</param>
        public Document(Database database, String documentId)
        {
            Database = database;
            Id = documentId;
        }

        #endregion

        #region Instance Members

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/> that owns this <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Database"/> that owns this <see cref="Couchbase.Lite.Document"/>.</value>
        public Database Database { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Document"/>'s id.
        /// </summary>
        /// <value>The <see cref="Couchbase.Lite.Document"/>'s id.</value>
        public String Id { get; set; }

        /// <summary>
        /// Gets if the <see cref="Couchbase.Lite.Document"/> is deleted.
        /// </summary>
        /// <value><c>true</c> if deleted; otherwise, <c>false</c>.</value>
        public Boolean Deleted { get { return CurrentRevision == null && LeafRevisions.Any (); } }

        /// <summary>
        /// If known, gets the Id of the current <see cref="Couchbase.Lite.Revision"/>, otherwise null.
        /// </summary>
        /// <value>The Id of the current <see cref="Couchbase.Lite.Revision"/> if known, otherwise null.</value>
        public String CurrentRevisionId {
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
                if (currentRevision == null) {
                    currentRevision = GetRevisionWithId(null);
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
                if (CurrentRevision == null)
                {
                    Log.W(Database.TAG, "get_RevisionHistory called but no CurrentRevision");
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

            Database.PurgeRevisions(docsToRevs);
            Database.RemoveDocumentFromCache(this);
        }

        /// <summary>
        /// Returns the <see cref="Couchbase.Lite.Revision"/> with the specified id if it exists, otherwise null.
        /// </summary>
        /// <param name="id">The <see cref="Couchbase.Lite.Revision"/> id.</param>
        /// <returns>The <see cref="Couchbase.Lite.Revision"/> with the specified id if it exists, otherwise null</returns>
        public SavedRevision GetRevision(String id)
        {
            if (CurrentRevision != null && id.Equals(CurrentRevision.Id))
                return CurrentRevision;

            var revisionInternal = Database.GetDocument(Id, id, true);

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
        public SavedRevision PutProperties(IDictionary<String, Object> properties)
        {
            var prevID = (string)properties.Get("_rev");
            return PutProperties(properties, prevID, allowConflict: false);
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
        /// Adds or Removed a change delegate that will be called whenever the Document changes
        /// </summary>
        public event EventHandler<DocumentChangeEventArgs> Change
        {
            add { _change = (EventHandler<DocumentChangeEventArgs>)Delegate.Combine(_change, value); }
            remove { _change = (EventHandler<DocumentChangeEventArgs>)Delegate.Remove(_change, value); }
        }
        private EventHandler<DocumentChangeEventArgs> _change;

        #endregion


        #region Non-public Members

        internal void ForgetCurrentRevision()
        {
            currentRevision = null;
        }

        private SavedRevision GetRevisionWithId(String revId)
        {
            if (!StringEx.IsNullOrWhiteSpace(revId) && revId.Equals(currentRevision.Id)) {
                return currentRevision;
            }

            return GetRevisionFromRev(Database.GetDocument(Id, revId, true));
        }

        internal void LoadCurrentRevisionFrom(QueryRow row)
        {
            if (row.DocumentRevisionId == null)
            {
                return;
            }
            var revId = row.DocumentRevisionId;
            if (currentRevision == null || RevIdGreaterThanCurrent(revId))
            {
                currentRevision = null;
                var properties = row.DocumentProperties;
                if (properties != null)
                {
                    var rev = new RevisionInternal(properties);
                    currentRevision = new SavedRevision(this, rev);
                }
            }
        }

        private bool RevIdGreaterThanCurrent(string revId)
        {
            return (RevisionInternal.CBLCompareRevIDs(revId, currentRevision.Id) > 0);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>       
        internal SavedRevision PutProperties(IDictionary<String, Object> properties, String prevID, Boolean allowConflict)
        {
            string newId = properties == null ? null : properties.GetCast<string>("_id");
            if (newId != null && !newId.Equals(Id, StringComparison.InvariantCultureIgnoreCase))  {
                Log.W(Database.TAG, String.Format("Trying to put wrong _id to this: {0} properties: {1}", this, properties)); // TODO: Make sure all string formats use .NET codes, and not Java.
            }

            // Process _attachments dict, converting CBLAttachments to dicts:
            IDictionary<string, object> attachments = null;
            if (properties != null && properties.ContainsKey("_attachments")) {
                attachments = properties.Get("_attachments").AsDictionary<string,object>();
            }

            if (attachments != null && attachments.Count > 0) {
                var updatedAttachments = Attachment.InstallAttachmentBodies(attachments, Database);
                properties["_attachments"] = updatedAttachments;
            }

            Status status = new Status();
            var newRev = Database.PutDocument(Id, properties, prevID, allowConflict, status);
            if (newRev == null) {
                throw new CouchbaseLiteException(status.Code);
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
            var revs = Database.GetAllDocumentRevisions(Id, true);
            foreach (RevisionInternal rev in revs)
            {
                // add it to result, unless we are not supposed to include deleted and it's deleted
                if (!includeDeleted && rev.IsDeleted())
                {
                }
                else
                {
                    // don't add it
                    result.Add(GetRevisionFromRev(rev));
                }
            }
            return Sharpen.Collections.UnmodifiableList(result);
        }

        internal SavedRevision GetRevisionFromRev(RevisionInternal internalRevision)
        {
            if (internalRevision == null) {
                return null;
            }

            if (currentRevision != null && internalRevision.GetRevId().Equals(CurrentRevision.Id)) {
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
                return;
            }

            // current revision didn't change
            if (currentRevision != null && !revId.Equals(currentRevision.Id))
            {
                var rev = documentChange.WinningRevisionIfKnown;
                if (rev == null || rev.IsDeleted()) {
                    currentRevision = null;
                } else if (!rev.IsDeleted()) {
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

            var changeEvent = _change;
            if (changeEvent != null)
                changeEvent(this, args);
        }

        #endregion

        #region Delegates

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

        #endregion

        #region EventArgs Subclasses
        /// <summary>
        /// The type of event raised when a <see cref="Couchbase.Lite.Document"/> changes. 
        /// This event is not raised in response to local <see cref="Couchbase.Lite.Document"/> changes.
        ///</summary>
        public class DocumentChangeEventArgs : EventArgs {

            //Properties
            /// <summary>
            /// Gets the <see cref="Couchbase.Lite.Document"/> that raised the event.
            /// </summary>
            /// <value>The <see cref="Couchbase.Lite.Document"/> that raised the event</value>
            public Document Source { get; internal set; }

            /// <summary>
            /// Gets the details of the change.
            /// </summary>
            /// <value>The details of the change.</value>
            public DocumentChange Change { get; internal set; }

        }

        #endregion

    }


}

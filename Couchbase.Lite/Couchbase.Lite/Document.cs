using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Couchbase.Lite.Util;
using Couchbase.Lite.Internal;
using Sharpen;
using System.Diagnostics;

namespace Couchbase.Lite {

    public partial class Document {

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

        /// <summary>Get the document's owning database.</summary>
        public Database Database { get; private set; }

        /// <summary>Get the document's ID</summary>
        public String Id { get; set; }

        /// <summary>Is this document deleted? (That is, does its current revision have the '_deleted' property?)
        ///     </summary>
        /// <returns>boolean to indicate whether deleted or not</returns>
        public Boolean Deleted { get { return CurrentRevision.IsDeletion; } }

        /// <summary>Get the ID of the current revision</summary>
        public String CurrentRevisionId {
            get {
                return CurrentRevision == null 
                    ? null
                    : CurrentRevision.Id;
            }
        }

        /// <summary>Get the current revision</summary>
        public SavedRevision CurrentRevision { get {
                return currentRevision ?? (currentRevision = GetRevision(null));
            }
        }

        /// <summary>Returns the document's history as an array of CBLRevisions.</summary>
        /// <remarks>Returns the document's history as an array of CBLRevisions. (See SavedRevision's method.)
        ///     </remarks>
        /// <returns>document's history</returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public IEnumerable<SavedRevision> RevisionHistory {
            get {
                if (CurrentRevision == null)
                {
                    Log.W(Database.Tag, "get_RevisionHistory called but no CurrentRevision");
                    return null;
                }
                return CurrentRevision.RevisionHistory;
            }
        }

        public IEnumerable<SavedRevision> ConflictingRevisions { get { return GetLeafRevisions(false); } }

        /// <summary>
        /// Gets all of the leaves in the  <see cref="Couchbase.Lite.Document"/>'s <see cref="Couchbase.Lite.Revision"/> tree.
        /// </summary>
        /// <remarks>
        /// Returns an error if an issue occurs while getting the leaf <see cref="Couchbase.Lite.Revision">s.
        /// </remarks>
        /// <value>The leaf revisions.</value>
        public IEnumerable<SavedRevision> LeafRevisions { get { return GetLeafRevisions(true); } }

        /// <summary>The contents of the current revision of the document.</summary>
        /// <remarks>
        /// The contents of the current revision of the document.
        /// This is shorthand for self.currentRevision.properties.
        /// Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
        /// </remarks>
        /// <returns>contents of the current revision of the document.</returns>
        public IDictionary<String, Object> Properties { get { return CurrentRevision.Properties; } }

        /// <summary>The user-defined properties, without the ones reserved by CouchDB.</summary>
        /// <remarks>
        /// The user-defined properties, without the ones reserved by CouchDB.
        /// This is based on -properties, with every key whose name starts with "_" removed.
        /// </remarks>
        /// <returns>user-defined properties, without the ones reserved by CouchDB.</returns>
        public IDictionary<String, Object> UserProperties { get { return CurrentRevision.UserProperties; } }

        /// <summary>Deletes this document by adding a deletion revision.</summary>
        /// <remarks>
        /// Deletes this document by adding a deletion revision.
        /// This will be replicated to other databases.
        /// </remarks>
        /// <returns>boolean to indicate whether deleted or not</returns>
        /// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public void Delete() { CurrentRevision.DeleteDocument(); }

        /// <summary>Purges this document from the database; this is more than deletion, it forgets entirely about it.
        ///     </summary>
        /// <remarks>
        /// Purges this document from the database; this is more than deletion, it forgets entirely about it.
        /// The purge will NOT be replicated to other databases.
        /// </remarks>
        /// <returns>boolean to indicate whether purged or not</returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public void Purge()
        {
            var revs = new List<String>();
            revs.Add("*");

            var docsToRevs = new Dictionary<String, IList<String>>();
            docsToRevs[Id] = revs;

            Database.PurgeRevisions(docsToRevs);
            Database.RemoveDocumentFromCache(this);
        }

        /// <summary>The revision with the specified ID.</summary>
        /// <param name="id">the revision ID</param>
        /// <returns>the SavedRevision object</returns>
        public SavedRevision GetRevision(String id)
        {
            if (id.Equals(CurrentRevision.Id))
                return CurrentRevision;

            var contentOptions = EnumSet.NoneOf<TDContentOptions>();
            var revisionInternal = Database.GetDocumentWithIDAndRev(Id, id, contentOptions);

            var revision = GetRevisionFromRev(revisionInternal);
            return revision;
        }

        /// <summary>
        /// Creates an unsaved new revision whose parent is the currentRevision,
        /// or which will be the first revision if the document doesn't exist yet.
        /// </summary>
        /// <remarks>
        /// Creates an unsaved new revision whose parent is the currentRevision,
        /// or which will be the first revision if the document doesn't exist yet.
        /// You can modify this revision's properties and attachments, then save it.
        /// No change is made to the database until/unless you save the new revision.
        /// </remarks>
        /// <returns>the newly created revision</returns>
        public UnsavedRevision CreateRevision()
        {
            return new UnsavedRevision(this, CurrentRevision);
        }

        /// <summary>
        /// Gets the properties of the current <see cref="Couchbase.Lite.Revision"/> of the <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <returns>The property.</returns>
        /// <param name="key">Key.</param>
        public Object GetProperty(String key) { return CurrentRevision.Properties.Get(key); }

        /// <summary>Saves a new revision.</summary>
        /// <remarks>
        /// Saves a new revision. The properties dictionary must have a "_rev" property
        /// whose ID matches the current revision's (as it will if it's a modified
        /// copy of this document's .properties property.)
        /// </remarks>
        /// <param name="properties">the contents to be saved in the new revision</param>
        /// <returns>a new SavedRevision</returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public SavedRevision PutProperties(IDictionary<String, Object> properties)
        {
            var prevID = (string)properties.Get("_rev");
            return PutProperties(properties, prevID);
        }

        /// <summary>Saves a new revision by letting the caller update the existing properties.
        ///     </summary>
        /// <remarks>
        /// Saves a new revision by letting the caller update the existing properties.
        /// This method handles conflicts by retrying (calling the block again).
        /// The DocumentUpdater implementation should modify the properties of the new revision and return YES to save or
        /// NO to cancel. Be careful: the DocumentUpdater can be called multiple times if there is a conflict!
        /// </remarks>
        /// <param name="updateDelegate">
        /// the callback DocumentUpdater implementation.  Will be called on each
        /// attempt to save. Should update the given revision's properties and then
        /// return YES, or just return NO to cancel.
        /// </param>
        /// <returns>The new saved revision, or null on error or cancellation.</returns>
        /// <exception cref="CouchbaseLiteException">CouchbaseLiteException</exception>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public SavedRevision Update(UpdateDelegate updateDelegate)
        {
            Debug.Assert(updateDelegate != null);

            var lastErrorCode = StatusCode.Unknown;
            do
            {
                UnsavedRevision newRev = CreateRevision();
                if (!updateDelegate(newRev))
                    break;
                try
                {
                    SavedRevision savedRev = newRev.Save();
                    if (savedRev != null)
                    {
                        return savedRev;
                    }
                }
                catch (CouchbaseLiteException e)
                {
                    lastErrorCode = e.GetCBLStatus().GetCode();
                }
            }
            while (lastErrorCode == StatusCode.Conflict);
            return null;
        }

        /// <summary>
        /// Fires whenever the <see cref="Couchbase.Lite.Document"/> changes.
        /// </summary>
        public event EventHandler<DocumentChangeEventArgs> Change;

    #endregion


    #region Non-public Members

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>       
        internal SavedRevision PutProperties(IDictionary<String, Object> properties, String prevID)
        {
            string newId = null;
            if (properties != null && properties.ContainsKey("_id"))
            {
                newId = (string)properties.Get("_id");
            }
            if (newId != null && !newId.Equals(Id, StringComparison.InvariantCultureIgnoreCase))
            {
                Log.W(Database.Tag, String.Format("Trying to put wrong _id to this: {0} properties: {1}", this, properties)); // TODO: Make sure all string formats use .NET codes, and not Java.
            }

            // Process _attachments dict, converting CBLAttachments to dicts:
            IDictionary<string, object> attachments = null;
            if (properties != null && properties.ContainsKey("_attachments"))
            {
                attachments = (IDictionary<string, object>)properties.Get("_attachments");
            }
            if (attachments != null && attachments.Count > 0)
            {
                IDictionary<string, object> updatedAttachments = Attachment.InstallAttachmentBodies
                                                                 (attachments, Database);
                properties.Put("_attachments", updatedAttachments);
            }

            var hasTrueDeletedProperty = false;
            if (properties != null)
            {
                hasTrueDeletedProperty = properties.Get("_deleted") != null && ((bool)properties.
                                                                                Get("_deleted"));
            }

            var deleted = (properties == null) || hasTrueDeletedProperty;
            var rev = new RevisionInternal(Id, null, deleted, Database);

            if (properties != null)
            {
                rev.SetProperties(properties);
            }

            var newRev = Database.PutRevision(rev, prevID, false);
            if (newRev == null)
            {
                return null;
            }
            return new SavedRevision(this, newRev);
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
            RevisionList revs = Database.GetAllRevisionsOfDocumentID(Id, true);
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
            if (internalRevision == null) return null;

            if (currentRevision != null && internalRevision.GetRevId().Equals(CurrentRevision.Id))
            {
                return currentRevision;
            }
            else
            {
                return new SavedRevision(this, internalRevision);
            }
        }

        internal void RevisionAdded(DocumentChange documentChange)
        {
            // TODO: in the iOS code, it calls CBL_Revision* rev = change.winningRevision;
            var rev = documentChange.RevisionInternal;
            if (currentRevision != null && !rev.GetRevId().Equals(currentRevision.Id))
            {
                currentRevision = new SavedRevision(this, rev);
            }
            var change = DocumentChange.TempFactory(rev, null);

            var args = new DocumentChangeEventArgs {
                Change = change,
                Source = this
            } ;

            var changeEvent = Change;
            if (changeEvent != null)
                changeEvent(this, args);
        }

    #endregion
    
    #region Delegates
        
        public delegate Boolean UpdateDelegate(UnsavedRevision revision);

    #endregion
    
    #region EventArgs Subclasses
        public class DocumentChangeEventArgs : EventArgs {

            //Properties
            public Document Source { get; internal set; }

            public DocumentChange Change { get; internal set; }

        }

    #endregion
    
    }


}


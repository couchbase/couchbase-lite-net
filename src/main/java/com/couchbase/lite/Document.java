package com.couchbase.lite;

import com.couchbase.lite.internal.CBLRevisionInternal;
import com.couchbase.lite.internal.InterfaceAudience;
import com.couchbase.lite.util.Log;

import java.util.ArrayList;
import java.util.Collections;
import java.util.EnumSet;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * A CouchbaseLite document (as opposed to any specific revision of it.)
 */
public class Document {

    /**
     * The document's owning database.
     */
    private Database database;

    /**
     * The document's ID.
     */
    private String documentId;

    /**
     * The current/latest revision. This object is cached.
     */
    private SavedRevision currentRevision;

    /**
     * Application-defined model object representing this document
     */
    private Object model;

    /**
     * Change Listeners
     */
    private List<ChangeListener> changeListeners = new ArrayList<ChangeListener>();

    /**
     * Constructor
     *
     * @param database   The document's owning database
     * @param documentId The document's ID
     */
    @InterfaceAudience.Private
    public Document(Database database, String documentId) {
        this.database = database;
        this.documentId = documentId;
    }

    /**
     * Get the document's owning database.
     */
    @InterfaceAudience.Public
    public Database getDatabase() {
        return database;
    }

    /**
     * Get the document's ID
     */
    @InterfaceAudience.Public
    public String getId() {
        return documentId;
    }

    /**
     * Is this document deleted? (That is, does its current revision have the '_deleted' property?)
     * @return boolean to indicate whether deleted or not
     */
    @InterfaceAudience.Public
    public boolean isDeleted() {
        return getCurrentRevision().isDeletion();
    }

    /**
     * Get the ID of the current revision
     */
    @InterfaceAudience.Public
    public String getCurrentRevisionId() {
        return getCurrentRevision().getId();
    }

    /**
     * Get the current revision
     */
    @InterfaceAudience.Public
    public SavedRevision getCurrentRevision() {
        if (currentRevision == null) {
            currentRevision = getRevisionWithId(null);
        }
        return currentRevision;
    }

    /**
     * Returns the document's history as an array of CBLRevisions. (See SavedRevision's method.)
     *
     * @return document's history
     * @throws CBLiteException
     */
    @InterfaceAudience.Public
    public List<SavedRevision> getRevisionHistory() throws CBLiteException {
        if (getCurrentRevision() == null) {
            Log.w(Database.TAG, "getRevisionHistory() called but no currentRevision");
            return null;
        }
        return getCurrentRevision().getRevisionHistory();
    }

    /**
     * Returns all the current conflicting revisions of the document. If the document is not
     * in conflict, only the single current revision will be returned.
     *
     * @return all current conflicting revisions of the document
     * @throws CBLiteException
     */
    @InterfaceAudience.Public
    public List<SavedRevision> getConflictingRevisions() throws CBLiteException {
        return getLeafRevisions(false);
    }

    /**
     * Returns all the leaf revisions in the document's revision tree,
     * including deleted revisions (i.e. previously-resolved conflicts.)
     *
     * @return all the leaf revisions in the document's revision tree
     * @throws CBLiteException
     */
    @InterfaceAudience.Public
    public List<SavedRevision> getLeafRevisions() throws CBLiteException {
        return getLeafRevisions(true);
    }

    /**
     * The contents of the current revision of the document.
     * This is shorthand for self.currentRevision.properties.
     * Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
     *
     * @return contents of the current revision of the document.
     */
    @InterfaceAudience.Public
    public Map<String,Object> getProperties() {
        return getCurrentRevision().getProperties();
    }

    /**
     * The user-defined properties, without the ones reserved by CouchDB.
     * This is based on -properties, with every key whose name starts with "_" removed.
     *
     * @return user-defined properties, without the ones reserved by CouchDB.
     */
    @InterfaceAudience.Public
    public Map<String,Object> getUserProperties() {
        return getCurrentRevision().getUserProperties();
    }

    /**
     * Deletes this document by adding a deletion revision.
     * This will be replicated to other databases.
     *
     * @return boolean to indicate whether deleted or not
     * @throws CBLiteException
     */
    @InterfaceAudience.Public
    public boolean delete() throws CBLiteException {
        return getCurrentRevision().deleteDocument() != null;
    }


    /**
     * Purges this document from the database; this is more than deletion, it forgets entirely about it.
     * The purge will NOT be replicated to other databases.
     *
     * @return boolean to indicate whether purged or not
     * @throws CBLiteException
     */
    @InterfaceAudience.Public
    public boolean purge() throws CBLiteException {
        Map<String, List<String>> docsToRevs = new HashMap<String, List<String>>();
        List<String> revs = new ArrayList<String>();
        revs.add("*");
        docsToRevs.put(documentId, revs);
        database.purgeRevisions(docsToRevs);
        database.removeDocumentFromCache(this);
        return true;
    }

    /**
     * The revision with the specified ID.
     *
     *
     * @param id the revision ID
     * @return the SavedRevision object
     */
    @InterfaceAudience.Public
    public SavedRevision getRevision(String id) {
        if (id.equals(currentRevision.getId())) {
            return currentRevision;
        }
        EnumSet<Database.TDContentOptions> contentOptions = EnumSet.noneOf(Database.TDContentOptions.class);
        CBLRevisionInternal revisionInternal = database.getDocumentWithIDAndRev(getId(), id, contentOptions);
        SavedRevision revision = null;
        revision = getRevisionFromRev(revisionInternal);
        return revision;
    }

    /**
     * Creates an unsaved new revision whose parent is the currentRevision,
     * or which will be the first revision if the document doesn't exist yet.
     * You can modify this revision's properties and attachments, then save it.
     * No change is made to the database until/unless you save the new revision.
     *
     * @return the newly created revision
     */
    @InterfaceAudience.Public
    public UnsavedRevision createRevision() {
        return new UnsavedRevision(this, getCurrentRevision());
    }

    /**
     * Shorthand for getProperties().get(key)
     */
    @InterfaceAudience.Public
    public Object getProperty(String key) {
        return getCurrentRevision().getProperties().get(key);
    }

    /**
     * Saves a new revision. The properties dictionary must have a "_rev" property
     * whose ID matches the current revision's (as it will if it's a modified
     * copy of this document's .properties property.)
     *
     * @param properties the contents to be saved in the new revision
     * @return a new SavedRevision
     */
    @InterfaceAudience.Public
    public SavedRevision putProperties(Map<String,Object> properties) throws CBLiteException {
        String prevID = (String) properties.get("_rev");
        return putProperties(properties, prevID);
    }

    /**
     * Saves a new revision by letting the caller update the existing properties.
     * This method handles conflicts by retrying (calling the block again).
     * The DocumentUpdater implementation should modify the properties of the new revision and return YES to save or
     * NO to cancel. Be careful: the DocumentUpdater can be called multiple times if there is a conflict!
     *
     * @param updater the callback DocumentUpdater implementation.  Will be called on each
     *                attempt to save. Should update the given revision's properties and then
     *                return YES, or just return NO to cancel.
     * @return The new saved revision, or null on error or cancellation.
     * @throws CBLiteException
     */
    @InterfaceAudience.Public
    public SavedRevision update(DocumentUpdater updater) throws CBLiteException {

        int lastErrorCode = CBLStatus.UNKNOWN;
        do {
            UnsavedRevision newRev = createRevision();
            if (updater.update(newRev) == false) {
                break;
            }
            try {
                SavedRevision savedRev = newRev.save();
                if (savedRev != null) {
                    return savedRev;
                }
            } catch (CBLiteException e) {
                lastErrorCode = e.getCBLStatus().getCode();
            }

        } while (lastErrorCode == CBLStatus.CONFLICT);
        return null;

    }


    @InterfaceAudience.Public
    public void addChangeListener(ChangeListener changeListener) {
        changeListeners.add(changeListener);
    }

    @InterfaceAudience.Public
    public void removeChangeListener(ChangeListener changeListener) {
        changeListeners.remove(changeListener);
    }

    /**
     * Gets a reference to an optional application-defined model object representing this Document.
     */
    @InterfaceAudience.Public
    public Object getModel() {
        return model;
    }

    /**
     * Sets a reference to an optional application-defined model object representing this Document.
     */
    @InterfaceAudience.Public
    public void setModel(Object model) {
        this.model = model;
    }

    /**
     * Get the document's abbreviated ID
     */
    public String getAbbreviatedId() {
        String abbreviated = documentId;
        if (documentId.length() > 10) {
            String firstFourChars = documentId.substring(0, 4);
            String lastFourChars = documentId.substring(abbreviated.length() - 4);
            return String.format("%s..%s", firstFourChars, lastFourChars);
        }
        return documentId;
    }


    List<SavedRevision> getLeafRevisions(boolean includeDeleted) throws CBLiteException {

        List<SavedRevision> result = new ArrayList<SavedRevision>();
        CBLRevisionList revs = database.getAllRevisionsOfDocumentID(documentId, true);
        for (CBLRevisionInternal rev : revs) {
            // add it to result, unless we are not supposed to include deleted and it's deleted
            if (!includeDeleted && rev.isDeleted()) {
                // don't add it
            }
            else {
                result.add(getRevisionFromRev(rev));
            }
        }
        return Collections.unmodifiableList(result);
    }



    SavedRevision putProperties(Map<String,Object> properties, String prevID) throws CBLiteException {
        String newId = null;
        if (properties != null && properties.containsKey("_id")) {
            newId = (String) properties.get("_id");
        }

        if (newId != null && !newId.equalsIgnoreCase(getId())) {
            Log.w(Database.TAG, String.format("Trying to put wrong _id to this: %s properties: %s", this, properties));
        }

        // Process _attachments dict, converting CBLAttachments to dicts:
        Map<String, Object> attachments = null;
        if (properties != null && properties.containsKey("__attachments")) {
            attachments = (Map<String, Object>) properties.get("_attachments");
        }
        if (attachments != null && attachments.size() > 0) {
            Map<String, Object> updatedAttachments = Attachment.installAttachmentBodies(attachments, database);
            properties.put("_attachments", updatedAttachments);
        }

        boolean hasTrueDeletedProperty = false;
        if (properties != null) {
            hasTrueDeletedProperty = properties.get("_deleted") != null && ((Boolean)properties.get("_deleted")).booleanValue();
        }
        boolean deleted = (properties == null) || hasTrueDeletedProperty;
        CBLRevisionInternal rev = new CBLRevisionInternal(documentId, null, deleted, database);
        if (properties != null) {
            rev.setProperties(properties);
        }
        CBLRevisionInternal newRev = database.putRevision(rev, prevID, false);
        if (newRev == null) {
            return null;
        }
        return new SavedRevision(this, newRev);

    }


    SavedRevision getRevisionFromRev(CBLRevisionInternal internalRevision) {
        if (internalRevision == null) {
            return null;
        }
        else if (currentRevision != null && internalRevision.getRevId().equals(currentRevision.getId())) {
            return currentRevision;
        }
        else {
            return new SavedRevision(this, internalRevision);
        }

    }

    SavedRevision getRevisionWithId(String revId) {
        if (revId != null && currentRevision != null && revId.equals(currentRevision.getId())) {
            return currentRevision;
        }
        return getRevisionFromRev(
                database.getDocumentWithIDAndRev(getId(),
                revId,
                EnumSet.noneOf(Database.TDContentOptions.class))
        );
    }


    public static interface DocumentUpdater {
        public boolean update(UnsavedRevision newRevision);
    }

    void loadCurrentRevisionFrom(QueryRow row) {
        if (row.getDocumentRevisionId() == null) {
            return;
        }
        String revId = row.getDocumentRevisionId();
        if (currentRevision == null || revIdGreaterThanCurrent(revId)) {
            Map<String, Object> properties = row.getDocumentProperties();
            if (properties != null) {
                CBLRevisionInternal rev = new CBLRevisionInternal(properties, row.getDatabase());
                currentRevision = new SavedRevision(this, rev);
            }
        }
     }

    private boolean revIdGreaterThanCurrent(String revId) {
        return (CBLRevisionInternal.CBLCompareRevIDs(revId, currentRevision.getId()) > 0);
    }

    void revisionAdded(Map<String,Object> changeNotification) {

        // TODO: the reason this is greyed out is that this should be called from Database.notifyChange()

        CBLRevisionInternal rev = (CBLRevisionInternal) changeNotification.get("rev");
        if (currentRevision != null && !rev.getRevId().equals(currentRevision.getId())) {
            currentRevision = new SavedRevision(this, rev);
        }
        DocumentChange change = DocumentChange.tempFactory(rev, null);
        for (ChangeListener listener : changeListeners) {
            listener.changed(new ChangeEvent(this, change));
        }

    }

    public static class ChangeEvent {
        private Document source;
        private DocumentChange change;

        public ChangeEvent(Document source, DocumentChange documentChange) {
            this.source = source;
            this.change = documentChange;
        }

        public Document getSource() {
            return source;
        }

        public DocumentChange getChange() {
            return change;
        }
    }

    public static interface ChangeListener {
        public void changed(ChangeEvent event);
    }

}

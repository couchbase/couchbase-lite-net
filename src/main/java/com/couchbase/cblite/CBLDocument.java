package com.couchbase.cblite;


import android.util.Log;

import com.couchbase.cblite.internal.CBLRevisionInternal;

import java.util.ArrayList;
import java.util.Collections;
import java.util.EnumSet;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * A CouchbaseLite document (as opposed to any specific revision of it.)
 */
public class CBLDocument {

    /**
     * The document's owning database.
     */
    private CBLDatabase database;

    /**
     * The document's ID.
     */
    private String documentId;

    /**
     * The current/latest revision. This object is cached.
     */
    private CBLRevision currentRevision;

    /**
     * Application-defined model object representing this document
     */
    private Object model;

    /**
     * Constructor
     *
     * @param database   The document's owning database
     * @param documentId The document's ID
     */
    public CBLDocument(CBLDatabase database, String documentId) {
        this.database = database;
        this.documentId = documentId;
    }

    /**
     * Get the document's owning database.
     */
    public CBLDatabase getDatabase() {
        return database;
    }

    /**
     * Get the document's ID
     */
    public String getId() {
        return documentId;
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

    /**
     * Is this document deleted? (That is, does its current revision have the '_deleted' property?)
     * @return boolean to indicate whether deleted or not
     */
    public boolean isDeleted() {
        return currentRevision.isDeleted();
    }

    /**
     * Deletes this document by adding a deletion revision.
     * This will be replicated to other databases.
     *
     * @return boolean to indicate whether deleted or not
     * @throws CBLiteException
     */
    public boolean delete() throws CBLiteException {
        return currentRevision.deleteDocument() != null;
    }

    /**
     * Purges this document from the database; this is more than deletion, it forgets entirely about it.
     * The purge will NOT be replicated to other databases.
     *
     * @return boolean to indicate whether purged or not
     * @throws CBLiteException
     */
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
     * @param revisionID the revision ID
     * @return the CBLRevision object
     */
    public CBLRevision getRevision(String revisionID) {
        if (revisionID.equals(currentRevision.getId())) {
            return currentRevision;
        }
        EnumSet<CBLDatabase.TDContentOptions> contentOptions = EnumSet.noneOf(CBLDatabase.TDContentOptions.class);
        CBLRevisionInternal revisionInternal = database.getDocumentWithIDAndRev(getId(), revisionID, contentOptions);
        CBLRevision revision = null;
        revision = getRevisionFromRev(revisionInternal);
        return revision;
    }

    /**
     * Get the current revision
     *
     * @return
     */
    public CBLRevision getCurrentRevision() {
        return currentRevision;
    }

    /**
     * Get the ID of the current revision
     *
     * @return
     */
    public String getCurrentRevisionId() {
        return currentRevision.getId();
    }

    /**
     * Returns the document's history as an array of CBLRevisions. (See CBLRevision's method.)
     *
     * @return document's history
     * @throws CBLiteException
     */
    public List<CBLRevision> getRevisionHistory() throws CBLiteException {
        if (currentRevision == null) {
            Log.w(CBLDatabase.TAG, "getRevisionHistory() called but no currentRevision");
            return null;
        }
        return currentRevision.getRevisionHistory();
    }

    /**
     * Returns all the current conflicting revisions of the document. If the document is not
     * in conflict, only the single current revision will be returned.
     *
     * @return all current conflicting revisions of the document
     * @throws CBLiteException
     */
    public List<CBLRevision> getConflictingRevisions() throws CBLiteException {
        return getLeafRevisions(false);
    }

    /**
     * Returns all the leaf revisions in the document's revision tree,
     * including deleted revisions (i.e. previously-resolved conflicts.)
     *
     * @return all the leaf revisions in the document's revision tree
     * @throws CBLiteException
     */
    public List<CBLRevision> getLeafRevisions() throws CBLiteException {
        return getLeafRevisions(true);
    }

    List<CBLRevision> getLeafRevisions(boolean includeDeleted) throws CBLiteException {

        List<CBLRevision> result = new ArrayList<CBLRevision>();
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

    /**
     * Creates an unsaved new revision whose parent is the currentRevision,
     * or which will be the first revision if the document doesn't exist yet.
     * You can modify this revision's properties and attachments, then save it.
     * No change is made to the database until/unless you save the new revision.
     *
     * @return the newly created revision
     */
    public CBLNewRevision newRevision() {
        return new CBLNewRevision(this, currentRevision);
    }

    /**
     * Shorthand for getProperties().get(key)
     */
    public Object propertyForKey(String key) {
        return currentRevision.getProperties().get(key);
    }

    /**
     * The contents of the current revision of the document.
     * This is shorthand for self.currentRevision.properties.
     * Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
     *
     * @return contents of the current revision of the document.
     */
    public Map<String,Object> getProperties() {
        return currentRevision.getProperties();
    }

    /**
     * The user-defined properties, without the ones reserved by CouchDB.
     * This is based on -properties, with every key whose name starts with "_" removed.
     *
     * @return user-defined properties, without the ones reserved by CouchDB.
     */
    public Map<String,Object> getUserProperties() {
        return currentRevision.getUserProperties();
    }

    /**
     * Saves a new revision. The properties dictionary must have a "_rev" property
     * whose ID matches the current revision's (as it will if it's a modified
     * copy of this document's .properties property.)
     *
     * @param properties the contents to be saved in the new revision
     * @return a new CBLRevision
     */
    public CBLRevision putProperties(Map<String,Object> properties) throws CBLiteException {
        String prevID = (String) properties.get("_rev");
        return putProperties(properties, prevID);
    }

    CBLRevision putProperties(Map<String,Object> properties, String prevID) throws CBLiteException {

        String newId = (String) properties.get("_id");
        if (newId != null && !newId.equalsIgnoreCase(getId())) {
            Log.w(CBLDatabase.TAG, String.format("Trying to put wrong _id to this: %s properties: %s", this, properties));
        }

        // Process _attachments dict, converting CBLAttachments to dicts:
        Map<String, Object> attachments = (Map<String, Object>) properties.get("_attachments");
        if (attachments != null && attachments.size() > 0) {
            Map<String, Object> updatedAttachments = CBLAttachment.installAttachmentBodies(attachments, database);
            properties.put("_attachments", updatedAttachments);
        }

        boolean deleted = (properties == null) || ((Boolean)properties.get("_deleted")).booleanValue();
        CBLRevisionInternal rev = new CBLRevisionInternal(documentId, null, deleted, database);
        if (properties != null) {
            rev.setProperties(properties);
        }
        CBLRevisionInternal newRev = database.putRevision(rev, prevID, false);
        if (newRev == null) {
            return null;
        }
        return new CBLRevision(this, newRev);

    }

    /**
     * Saves a new revision by letting the caller update the existing properties.
     * This method handles conflicts by retrying (calling the block again).
     * The CBLRevisionUpdater implementation should modify the properties of the new revision and return YES to save or
     * NO to cancel. Be careful: the CBLRevisionUpdater can be called multiple times if there is a conflict!
     *
     * @param updater the callback CBLRevisionUpdater implementation.  Will be called on each
     *                attempt to save. Should update the given revision's properties and then
     *                return YES, or just return NO to cancel.
     * @return The new saved revision, or null on error or cancellation.
     * @throws CBLiteException
     */
    public CBLRevision update(CBLRevisionUpdater updater) throws CBLiteException {

        int lastErrorCode = CBLStatus.UNKNOWN;
        do {
            CBLNewRevision newRev = newRevision();
            if (updater.updateRevision(newRev) == false) {
                break;
            }
            try {
                CBLRevision savedRev = newRev.save();
                if (savedRev != null) {
                    return savedRev;
                }
            } catch (CBLiteException e) {
                lastErrorCode = e.getCBLStatus().getCode();
            }

        } while (lastErrorCode == CBLStatus.CONFLICT);
        return null;

    }

    CBLRevision getRevisionFromRev(CBLRevisionInternal internalRevision) {
        if (internalRevision == null) {
            return null;
        }
        else if (internalRevision.getRevId().equals(currentRevision.getId())) {
            return currentRevision;
        }
        else {
            return new CBLRevision(this, internalRevision);
        }

    }

    public Object getModel() {
        return model;
    }

    public void setModel(Object model) {
        this.model = model;
    }

    public static interface CBLRevisionUpdater {
        public boolean updateRevision(CBLNewRevision newRevision);
    }

    /*
    - (void) loadCurrentRevisionFrom: (CBLQueryRow*)row {
    NSString* revID = row.documentRevision;
    if (!revID)
        return;
    if (!_currentRevision || CBLCompareRevIDs(revID, _currentRevision.revisionID) > 0) {
        [self forgetCurrentRevision];
        NSDictionary* properties = row.documentProperties;
        if (properties) {
            CBL_Revision* rev = [CBL_Revision revisionWithProperties: properties];
            _currentRevision = [[CBLRevision alloc] initWithDocument: self revision: rev];
        }
    }
}
     */

    void loadCurrentRevisionFrom(CBLQueryRow row) {
        if (row.getDocumentRevision() == null) {
            return;
        }
        // TODO: finish porting
        /*if (currentRevision == null || ) {

        }*/
     }


}

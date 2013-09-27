package com.couchbase.cblite;


import android.util.Log;

import com.couchbase.cblite.internal.CBLRevisionInternal;

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
    private String documentID;

    /**
     * The current/latest revision. This object is cached.
     */
    private CBLRevision currentRevision;

    /**
     * Constructor
     *
     * @param database   The document's owning database
     * @param documentId The document's ID
     */
    public CBLDocument(CBLDatabase database, String documentId) {
        this.database = database;
        this.documentID = documentId;
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
    public String getDocumentID() {
        return documentID;
    }

    /**
     * Get the document's abbreviated ID
     */
    public String getAbbreviatedID() {
        // TODO: implement
        throw new RuntimeException("Not Implemented");
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
        // TODO: implement
        throw new RuntimeException("Not Implemented");
    }

    /**
     * Purges this document from the database; this is more than deletion, it forgets entirely about it.
     * The purge will NOT be replicated to other databases.
     *
     * @return boolean to indicate whether purged or not
     * @throws CBLiteException
     */
    public boolean purge() throws CBLiteException {
        // TODO: implement
        throw new RuntimeException("Not Implemented");
    }

    /**
     * The revision with the specified ID.
     *
     * @param revisionID the revision ID
     * @return the CBLRevision object
     */
    public CBLRevision revisionWithID(String revisionID) {
        if (revisionID.equals(currentRevision.getRevId())) {
            return currentRevision;
        }
        // TODO: implement
        throw new RuntimeException("Not Implemented");
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
    public List<String> getConflictingRevisions() throws CBLiteException {
        return database.getConflictingRevisionIDsOfDocID(documentID);
    }

    /**
     * Returns all the leaf revisions in the document's revision tree,
     * including deleted revisions (i.e. previously-resolved conflicts.)
     *
     * @return all the leaf revisions in the document's revision tree
     * @throws CBLiteException
     */
    public List<CBLRevision> getLeafRevisions() throws CBLiteException {
        // TODO: implement
        throw new RuntimeException("Not Implemented");
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
        // TODO: implement
        throw new RuntimeException("Not Implemented");
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
    public CBLRevision putProperties(Map<String,Object> properties) {
        String prevID = (String) properties.get("_rev");

        return putProperties(properties, prevID);
    }

    CBLRevision putProperties(Map<String,Object> properties, String prevID) {

        // TODO: here is the objectivec impl

        /*

            id idProp = [properties objectForKey: @"_id"];
            if (idProp && ![idProp isEqual: self.documentID])
                Warn(@"Trying to PUT wrong _id to %@: %@", self, properties);

            // Process _attachments dict, converting CBLAttachments to dicts:
            NSDictionary* attachments = properties[@"_attachments"];
            if (attachments.count) {
                NSDictionary* expanded = [CBLAttachment installAttachmentBodies: attachments
                                                                     intoDatabase: _database];
                if (expanded != attachments) {
                    NSMutableDictionary* nuProperties = [properties mutableCopy];
                    nuProperties[@"_attachments"] = expanded;
                    properties = nuProperties;
                }
            }

            BOOL deleted = !properties || [properties[@"_deleted"] boolValue];
            CBL_MutableRevision* rev = [[CBL_MutableRevision alloc] initWithDocID: _docID
                                                                            revID: nil
                                                                          deleted: deleted];
            if (properties)
                rev.properties = properties;
            CBLStatus status = 0;
            CBL_Revision* newRev = [_database putRevision: rev prevRevisionID: prevID
                                            allowConflict: NO status: &status];
            if (!newRev) {
                if (outError) *outError = CBLStatusToNSError(status, nil);
                return nil;
            }
            return [[CBLRevision alloc] initWithDocument: self revision: newRev];

         */
        return null;
    }


    /**
     * Saves a new revision by letting the caller update the existing properties.
     * This method handles conflicts by retrying (calling the block again).
     * The CBLRevisionUpdater implementation should modify the properties of the new revision and return YES to save or
     * NO to cancel. Be careful: the block can be called multiple times if there is a conflict!
     *
     * @param updater the callback CBLRevisionUpdater implementation.  Will be called on each
     *                attempt to save. Should update the given revision's properties and then
     *                return YES, or just return NO to cancel.
     * @return The new saved revision, or null on error or cancellation.
     * @throws CBLiteException
     */
    public CBLRevision update(CBLRevisionUpdater updater) throws CBLiteException {
        // TODO: implement
        throw new RuntimeException("Not Implemented");
    }

    CBLRevision getRevisionFromRev(CBLRevisionInternal internalRevision) throws CBLiteException {
        if (internalRevision == null) {
            return null;
        }
        else if (internalRevision.getRevId().equals(currentRevision.getRevId())) {
            return currentRevision;
        }
        else {
            return new CBLRevision(this, internalRevision);
        }

    }


    public static interface CBLRevisionUpdater {
        public boolean updateRevision(CBLNewRevision newRevision);
    }


}

package com.couchbase.cblite;

import com.couchbase.cblite.internal.InterfaceAudience;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Stores information about a revision -- its docID, revID, and whether it's deleted.
 *
 * It can also store the sequence number and document contents (they can be added after creation).
 */
public abstract class CBLRevision {


    /**
     * The sequence number of this revision.
     */
    protected long sequence;

    /**
     * The revisions's owning database.
     */
    protected CBLDatabase database;

    /**
     * The document this is a revision of
     */
    protected CBLDocument document;

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    CBLRevision() {
        super();
    }

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    protected CBLRevision(CBLDocument document) {
        this.document = document;
    }

    /**
     * Get the revision's owning database.
     */
    @InterfaceAudience.Public
    public CBLDatabase getDatabase() {
        return database;
    }

    /**
     * Get the document this is a revision of.
     */
    @InterfaceAudience.Public
    public CBLDocument getDocument() {
        return document;
    }

    /**
     * Gets the Revision's id.
     */

    @InterfaceAudience.Public
    public abstract String getId();


    /**
     * Does this revision mark the deletion of its document?
     * (In other words, does it have a "_deleted" property?)
     */
    @InterfaceAudience.Public
    boolean isDeletion() {
        Object deleted = getProperty("_deleted");
        if (deleted == null) {
            return false;
        }
        Boolean deletedBool = (Boolean) deleted;
        return deletedBool.booleanValue();
    }

    /**
     * The contents of this revision of the document.
     * Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
     *
     * @return contents of this revision of the document.
     */
    @InterfaceAudience.Public
    public abstract Map<String,Object> getProperties();


    /**
     * The user-defined properties, without the ones reserved by CouchDB.
     * This is based on -properties, with every key whose name starts with "_" removed.
     *
     * @return user-defined properties, without the ones reserved by CouchDB.
     */
    @InterfaceAudience.Public
    public Map<String,Object> getUserProperties() {

        Map<String,Object> result = new HashMap<String, Object>();

        Map<String,Object> sourceMap = getProperties();
        for (String key : sourceMap.keySet()) {
            if (!key.startsWith("_")) {
                result.put(key, sourceMap.get(key));
            }
        }
        return Collections.unmodifiableMap(result);
    }

    /**
     * The names of all attachments
     * @return
     */
    @InterfaceAudience.Public
    public List<String> getAttachmentNames() {
        Map<String, Object> attachmentMetadata = getAttachmentMetadata();
        ArrayList<String> result = new ArrayList<String>();
        result.addAll(attachmentMetadata.keySet());
        return result;
    }

    /**
     * All attachments, as CBLAttachment objects.
     */
    @InterfaceAudience.Public
    public List<CBLAttachment> getAttachments() {
        List<CBLAttachment> result = new ArrayList<CBLAttachment>();
        List<String> attachmentNames = getAttachmentNames();
        for (String attachmentName : attachmentNames) {
            result.add(getAttachment(attachmentName));
        }
        return result;
    }

    /**
     * Shorthand for getProperties().get(key)
     */
    @InterfaceAudience.Public
    public Object getProperty(String key) {
        return getProperties().get(key);
    }

    /**
     * Looks up the attachment with the given name (without fetching its contents yet).
     */
    @InterfaceAudience.Public
    public CBLAttachment getAttachment(String name) {
        Map<String, Object> attachmentMetadata = getAttachmentMetadata();
        if (attachmentMetadata == null) {
            return null;
        }
        return new CBLAttachment(this, name, attachmentMetadata);
    }



    Map<String, Object> getAttachmentMetadata() {
        return (Map<String, Object>) getProperty("_attachments");
    }

    @Override
    public boolean equals(Object o) {
        boolean result = false;
        if(o instanceof CBLSavedRevision) {
            CBLSavedRevision other = (CBLSavedRevision)o;
            if(document.getId().equals(other.getDocument().getId()) && getId().equals(other.getId())) {
                result = true;
            }
        }
        return result;
    }

    @Override
    public int hashCode() {
        return document.getId().hashCode() ^ getId().hashCode();
    }


    void setSequence(long sequence) {
        this.sequence = sequence;
    }

    long getSequence() {
        return sequence;
    }

    @Override
    public String toString() {
        return "{" + this.document.getId() + " #" + this.getId() + (isDeletion() ? "DEL" : "") + "}";
    }

    /**
     * Generation number: 1 for a new document, 2 for the 2nd revision, ...
     * Extracted from the numeric prefix of the revID.
     */
    int getGeneration() {
        return generationFromRevID(getId());
    }

    static int generationFromRevID(String revID) {
        int generation = 0;
        int dashPos = revID.indexOf("-");
        if(dashPos > 0) {
            generation = Integer.parseInt(revID.substring(0, dashPos));
        }
        return generation;
    }

}

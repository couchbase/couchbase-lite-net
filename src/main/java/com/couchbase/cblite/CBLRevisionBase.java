package com.couchbase.cblite;

import java.util.List;
import java.util.Map;

/**
 * Stores information about a revision -- its docID, revID, and whether it's deleted.
 *
 * It can also store the sequence number and document contents (they can be added after creation).
 */
public class CBLRevisionBase {

    /**
     * The ID of this revision. Will be nil if this is an unsaved CBLNewRevision.
     */
    protected String revId;

    /**
     * Does this revision mark the deletion of its document?
     * (In other words, does it have a "_deleted" property?)
     */
    protected boolean deleted;


    /**
     * The request/response/document body, stored as either JSON or a Map<String,Object>
     */
    protected CBLBody checkedBody;


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
    CBLRevisionBase() {
        super();
    }

    /**
     * Constructor
     *
     * @param document
     */
    protected CBLRevisionBase(CBLDocument document) {
        this.document = document;
    }

    /**
     * Get the revision's owning database.
     */
    public CBLDatabase getDatabase() {
        return database;
    }

    /**
     * Get the document this is a revision of
     */
    public CBLDocument getDocument() {
        return document;
    }

    /**
     * The contents of this revision of the document.
     * Any keys in the dictionary that begin with "_", such as "_id" and "_rev", contain CouchbaseLite metadata.
     *
     * TODO: this is returning a mutable map, which is a misleading API
     *
     * @return contents of this revision of the document.
     */
    public Map<String,Object> getProperties() {
        Map<String,Object> result = null;
        if(checkedBody != null) {
            result = checkedBody.getProperties();
        }
        return result;
    }

    /**
     * Shorthand for getProperties().get(key)
     */
    public Object getPropertyForKey(String key) {
        return getProperties().get(key);
    }

    /**
     * The user-defined properties, without the ones reserved by CouchDB.
     * This is based on -properties, with every key whose name starts with "_" removed.
     *
     * @return user-defined properties, without the ones reserved by CouchDB.
     */
    public Map<String,Object> getUserProperties() {
        // TODO
        throw new RuntimeException("Not implemented");
    }

    /**
     * The names of all attachments
     * @return
     */
    public List<String> getAttachmentNames() {
        // TODO
        throw new RuntimeException("Not implemented");
    }

    /**
     * Looks up the attachment with the given name (without fetching its contents yet).
     *
     * @param name
     * @return
     */
    public CBLAttachment getAttachmentNamed(String name) {
        // TODO
        throw new RuntimeException("Not implemented");
    }

    /**
     * All attachments, as CBLAttachment objects.
     * @return
     */
    public List<CBLAttachment> getAttachments() {
        // TODO
        throw new RuntimeException("Not implemented");
    }

    void setProperties(Map<String,Object> properties) {
        this.checkedBody = new CBLBody(properties);
    }

    @Override
    public boolean equals(Object o) {
        boolean result = false;
        if(o instanceof CBLRevision) {
            CBLRevision other = (CBLRevision)o;
            if(document.getDocumentID().equals(other.getDocument().getDocumentID()) && revId.equals(other.revId)) {
                result = true;
            }
        }
        return result;
    }

    @Override
    public int hashCode() {
        return document.getDocumentID().hashCode() ^ revId.hashCode();
    }

    byte[] getJson() {
        byte[] result = null;
        if(checkedBody != null) {
            result = checkedBody.getJson();
        }
        return result;
    }

    void setJson(byte[] json) {
        this.checkedBody = new CBLBody(json);
    }


    public String getRevId() {
        return revId;
    }

    void setRevId(String revId) {
        this.revId = revId;
    }

    boolean isDeleted() {
        return deleted;
    }

    void setDeleted(boolean deleted) {
        this.deleted = deleted;
    }

    CBLBody getBody() {
        return checkedBody;
    }

    void setBody(CBLBody body) {
        this.checkedBody = body;
    }

    void setSequence(long sequence) {
        this.sequence = sequence;
    }

    long getSequence() {
        return sequence;
    }

    @Override
    public String toString() {
        return "{" + this.document.getDocumentID() + " #" + this.revId + (deleted ? "DEL" : "") + "}";
    }

    /**
     * Generation number: 1 for a new document, 2 for the 2nd revision, ...
     * Extracted from the numeric prefix of the revID.
     */
    int getGeneration() {
        return generationFromRevID(revId);
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

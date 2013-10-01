package com.couchbase.cblite;

import com.couchbase.cblite.internal.CBLBody;

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
public class CBLRevisionBase {

    /**
     * The ID of this revision. Will be nil if this is an unsaved CBLNewRevision.
     */
    protected String revId;

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
     * @return contents of this revision of the document.
     */
    public Map<String,Object> getProperties() {
        Map<String,Object> result = null;
        if(checkedBody != null) {
            result = checkedBody.getProperties();
        }
        return Collections.unmodifiableMap(result);
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

        Map<String,Object> result = new HashMap<String, Object>();

        if(checkedBody != null) {
            Map<String,Object> sourceMap = checkedBody.getProperties();
            for (String key : sourceMap.keySet()) {
                if (!key.startsWith("_")) {
                    result.put(key, sourceMap.get(key));
                }
            }

        }
        return Collections.unmodifiableMap(result);
    }

    void setProperties(Map<String,Object> properties) {
        this.checkedBody = new CBLBody(properties);
    }

    /**
     * The names of all attachments
     * @return
     */
    public List<String> getAttachmentNames() {
        Map<String, Object> attachmentMetadata = getAttachmentMetadata();
        ArrayList<String> result = new ArrayList<String>();
        result.addAll(attachmentMetadata.keySet());
        return result;
    }

    /**
     * Looks up the attachment with the given name (without fetching its contents yet).
     *
     * @param name
     * @return
     */
    public CBLAttachment getAttachment(String name) {
        Map<String, Object> attachmentMetadata = getAttachmentMetadata();
        if (attachmentMetadata == null) {
            return null;
        }
        return new CBLAttachment(this, name, attachmentMetadata);
    }

    /**
     * All attachments, as CBLAttachment objects.
     * @return
     */
    public List<CBLAttachment> getAttachments() {
        List<CBLAttachment> result = new ArrayList<CBLAttachment>();
        List<String> attachmentNames = getAttachmentNames();
        for (String attachmentName : attachmentNames) {
            result.add(getAttachment(attachmentName));
        }
        return result;
    }



    Map<String, Object> getAttachmentMetadata() {
        return (Map<String, Object>) getPropertyForKey("_attachments");
    }

    @Override
    public boolean equals(Object o) {
        boolean result = false;
        if(o instanceof CBLRevision) {
            CBLRevision other = (CBLRevision)o;
            if(document.getId().equals(other.getDocument().getId()) && revId.equals(other.revId)) {
                result = true;
            }
        }
        return result;
    }

    @Override
    public int hashCode() {
        return document.getId().hashCode() ^ revId.hashCode();
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


    public String getId() {
        return revId;
    }

    void setId(String revId) {
        this.revId = revId;
    }

    /**
     * Does this revision mark the deletion of its document?
     * (In other words, does it have a "_deleted" property?)
     */
    boolean isDeleted() {
        Object deleted = getPropertyForKey("_deleted");
        if (deleted == null) {
            return false;
        }
        Boolean deletedBool = (Boolean) deleted;
        return deletedBool.booleanValue();
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
        return "{" + this.document.getId() + " #" + this.revId + (isDeleted() ? "DEL" : "") + "}";
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

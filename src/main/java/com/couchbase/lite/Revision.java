package com.couchbase.lite;

import com.couchbase.lite.internal.InterfaceAudience;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Stores information about a revision -- its docID, revID, and whether it's deleted.
 *
 * It can also store the sequence number and document contents (they can be added after creation).
 */
public abstract class Revision {

    /**
     * The sequence number of this revision.
     */
    protected long sequence;

    /**
     * The document this is a revision of
     */
    protected Document document;

    /**
     * The ID of the parentRevision.
     */
    protected String parentRevID;

    /**
     * The revision this one is a child of.
     */
    protected SavedRevision parentRevision;

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    Revision() {
        super();
    }

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    protected Revision(Document document) {
        this.document = document;
    }

    /**
     * Get the revision's owning database.
     */
    @InterfaceAudience.Public
    public Database getDatabase() {
        return document.getDatabase();
    }

    /**
     * Get the document this is a revision of.
     */
    @InterfaceAudience.Public
    public Document getDocument() {
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
    public boolean isDeletion() {
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
        return result;
    }

    /**
     * The names of all attachments
     * @return
     */
    @InterfaceAudience.Public
    public List<String> getAttachmentNames() {
        Map<String, Object> attachmentMetadata = getAttachmentMetadata();
        ArrayList<String> result = new ArrayList<String>();
        if (attachmentMetadata != null) {
            result.addAll(attachmentMetadata.keySet());
        }
        return result;
    }

    /**
     * All attachments, as Attachment objects.
     */
    @InterfaceAudience.Public
    public List<Attachment> getAttachments() {
        List<Attachment> result = new ArrayList<Attachment>();
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
    public Attachment getAttachment(String name) {
        Map<String, Object> attachmentsMetadata = getAttachmentMetadata();
        if (attachmentsMetadata == null) {
            return null;
        }
        Map<String, Object> attachmentMetadata = (Map<String, Object>) attachmentsMetadata.get(name);
        return new Attachment(this, name, attachmentMetadata);
    }

    @InterfaceAudience.Public
    public abstract SavedRevision getParentRevision();

    @InterfaceAudience.Public
    public abstract String getParentRevisionId();

    /**
     * Returns the history of this document as an array of CBLRevisions, in forward order.
     * Older revisions are NOT guaranteed to have their properties available.
     *
     * @throws CouchbaseLiteException
     */
    @InterfaceAudience.Public
    public abstract List<SavedRevision> getRevisionHistory() throws CouchbaseLiteException;

    @Override
    @InterfaceAudience.Public
    public boolean equals(Object o) {
        boolean result = false;
        if(o instanceof SavedRevision) {
            SavedRevision other = (SavedRevision)o;
            if(document.getId().equals(other.getDocument().getId()) && getId().equals(other.getId())) {
                result = true;
            }
        }
        return result;
    }

    @Override
    @InterfaceAudience.Public
    public int hashCode() {
        return document.getId().hashCode() ^ getId().hashCode();
    }

    @Override
    @InterfaceAudience.Public
    public String toString() {
        return "{" + this.document.getId() + " #" + this.getId() + (isDeletion() ? "DEL" : "") + "}";
    }

    @InterfaceAudience.Private
    /* package */ Map<String, Object> getAttachmentMetadata() {
        return (Map<String, Object>) getProperty("_attachments");
    }

    @InterfaceAudience.Private
    /* package */ void setSequence(long sequence) {
        this.sequence = sequence;
    }

    @InterfaceAudience.Private
    /* package */ long getSequence() {
        return sequence;
    }

    /**
     * Generation number: 1 for a new document, 2 for the 2nd revision, ...
     * Extracted from the numeric prefix of the revID.
     */
    @InterfaceAudience.Private
    /* package */ int getGeneration() {
        return generationFromRevID(getId());
    }

    @InterfaceAudience.Private
    /* package */ static int generationFromRevID(String revID) {
        int generation = 0;
        int dashPos = revID.indexOf("-");
        if(dashPos > 0) {
            generation = Integer.parseInt(revID.substring(0, dashPos));
        }
        return generation;
    }

}

package com.couchbase.cblite;

import com.couchbase.cblite.internal.InterfaceAudience;

import java.util.HashMap;
import java.util.Map;

public class CBLUnsavedRevision extends CBLRevision {

    private String parentRevID;
    private Map<String, Object> properties;

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    protected CBLUnsavedRevision(CBLDocument document, CBLSavedRevision parentRevision) {

        super(document);

        parentRevID = parentRevision.getId();

        Map<String, Object> parentRevisionProperties = parentRevision.getProperties();

        if (parentRevisionProperties == null) {
            properties = new HashMap<String, Object>();
            properties.put("_id", document.getId());
            properties.put("_rev", parentRevID);
        }
        else {
            properties = new HashMap<String, Object>(parentRevisionProperties);
        }

    }

    /**
     * Set whether this revision is a deletion or not (eg, marks doc as deleted)
     */
     @InterfaceAudience.Public
     public void setIsDeletion(boolean isDeletion) {
        if (isDeletion == true) {
            properties.put("_deleted", true);
        }
        else {
            properties.remove("_deleted");
        }
    }

    /**
     * Get the id of the owning document.  In the case of an unsaved revision, may return null.
     * @return
     */
    @Override
    @InterfaceAudience.Public
    public String getId() {
        if (properties != null) {
            return (String) properties.get("_id");
        }
        return null;
    }

    /**
     * Set the properties for this revision
     */
    @InterfaceAudience.Public
    public void setProperties(Map<String,Object> properties) {
        this.properties = properties;
    }

    /**
     * Saves the new revision to the database.
     *
     * This will throw an exception with a 412 error if its parent (the revision it was created from)
     * is not the current revision of the document.
     *
     * Afterwards you should use the returned CBLRevision instead of this object.
     *
     * @return A new CBLRevision representing the saved form of the revision.
     * @throws CBLiteException
     */
    @InterfaceAudience.Public
    public CBLSavedRevision save() throws CBLiteException {
        return document.putProperties(properties, parentRevID);
    }

    /**
     * Creates or updates an attachment.
     * The attachment data will be written to the database when the revision is saved.
     * @param attachment A newly-created CBLAttachment (not yet associated with any revision)
     * @param name The attachment name.
     */
    @InterfaceAudience.Public
    public void addAttachment(CBLAttachment attachment, String name) {
        Map<String, Object> attachments =  (Map<String, Object>) properties.get("_attachments");
        attachments.put(name, attachment);
        properties.put("_attachments", attachments);
        attachment.setName(name);
        attachment.setRevision(this);
    }

    /**
     * Deletes any existing attachment with the given name.
     * The attachment will be deleted from the database when the revision is saved.
     * @param name The attachment name.
     */
    @InterfaceAudience.Public
    public void deleteAttachment(String name) {
        addAttachment(null, name);
    }

    @Override
    @InterfaceAudience.Public
    public Map<String, Object> getProperties() {
        return properties;
    }


    // TODO: move this up to base class
    // TODO: see https://github.com/couchbaselabs/couchbase-lite-api/issues/5
    public CBLSavedRevision getParentRevision() {
        if (parentRevID == null || parentRevID.length() == 0) {
            return null;
        }
        return document.getRevision(parentRevID);
    }

    // TODO: move this up to base class
    // TODO: see https://github.com/couchbaselabs/couchbase-lite-api/issues/5
    public String getParentRevisionId() {
        return parentRevID;
    }




}

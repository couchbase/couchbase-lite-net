package com.couchbase.lite;

import com.couchbase.lite.internal.InterfaceAudience;
import com.couchbase.lite.util.Log;

import java.io.IOException;
import java.io.InputStream;
import java.net.URL;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * An unsaved Couchbase Lite Document Revision.
 */
public class UnsavedRevision extends Revision {

    private Map<String, Object> properties;

    /**
     * Constructor
     * @exclude
     */
    @InterfaceAudience.Private
    /* package */ protected UnsavedRevision(Document document, SavedRevision parentRevision) {

        super(document);

        if (parentRevision == null) {
            parentRevID = null;
        } else {
            parentRevID = parentRevision.getId();
        }

        Map<String, Object> parentRevisionProperties;

        if (parentRevision == null) {
            parentRevisionProperties = null;
        } else {
            parentRevisionProperties = parentRevision.getProperties();
        }

        if (parentRevisionProperties == null) {
            properties = new HashMap<String, Object>();
            properties.put("_id", document.getId());
            if (parentRevID != null) {
                properties.put("_rev", parentRevID);
            }
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
     * Afterwards you should use the returned Revision instead of this object.
     *
     * @return A new Revision representing the saved form of the revision.
     * @throws CouchbaseLiteException
     */
    @InterfaceAudience.Public
    public SavedRevision save() throws CouchbaseLiteException {
        boolean allowConflict = false;
        return document.putProperties(properties, parentRevID, allowConflict);
    }

    /**
     * A special variant of -save: that always adds the revision, even if its parent is not the
     * current revision of the document.
     *
     * This can be used to resolve conflicts, or to create them. If you're not certain that's what you
     * want to do, you should use the regular -save: method instead.
     */
    @InterfaceAudience.Public
    public SavedRevision save(boolean allowConflict) throws CouchbaseLiteException {
        return document.putProperties(properties, parentRevID, allowConflict);
    }

    /**
     * Creates or updates an attachment.
     * The attachment data will be written to the database when the revision is saved.
     * @param attachment A newly-created Attachment (not yet associated with any revision)
     * @param name The attachment name.
     */
    @InterfaceAudience.Public
    public void addAttachment(Attachment attachment, String name) {
        Map<String, Object> attachments =  (Map<String, Object>) properties.get("_attachments");
        if (attachments == null) {
            attachments = new HashMap<String, Object>();
        }
        attachments.put(name, attachment);
        properties.put("_attachments", attachments);
        if (attachment != null) {
            attachment.setName(name);
            attachment.setRevision(this);
        }
    }

    /**
     * Deletes any existing attachment with the given name.
     * The attachment will be deleted from the database when the revision is saved.
     * @param name The attachment name.
     */
    @InterfaceAudience.Public
    public void removeAttachment(String name) {
        addAttachment(null, name);
    }

    /**
     * Sets the userProperties of the Revision.
     * Set replaces all properties except for those with keys prefixed with '_'.
     */
    @InterfaceAudience.Public
    public void setUserProperties(Map<String,Object> userProperties) {
        Map<String, Object> newProps = new HashMap<String, Object>();
        newProps.putAll(userProperties);
        for (String key : properties.keySet()) {
            if (key.startsWith("_")) {
                newProps.put(key, properties.get(key));  // Preserve metadata properties
            }
        }
        properties = newProps;
    }

    /**
     * Sets the attachment with the given name. The Attachment data will be written to the Database when the Revision is saved.
     *
     * @param name The name of the Attachment to set.
     * @param contentType The content-type of the Attachment.
     * @param contentStream The Attachment content.  The InputStream will be closed after it is no longer needed.
     */
    @InterfaceAudience.Public
    public void setAttachment(String name, String contentType, InputStream contentStream) {
        Attachment attachment = new Attachment(contentStream, contentType);
        addAttachment(attachment, name);
    }

    /**
     * Sets the attachment with the given name. The Attachment data will be written to the Database when the Revision is saved.
     *
     * @param name The name of the Attachment to set.
     * @param contentType The content-type of the Attachment.
     * @param contentStreamURL The URL that contains the Attachment content.
     */
    @InterfaceAudience.Public
    public void setAttachment(String name, String contentType, URL contentStreamURL) {
        try {
            InputStream inputStream = contentStreamURL.openStream();
            setAttachment(name, contentType, inputStream);
        } catch (IOException e) {
            Log.e(Database.TAG, "Error opening stream for url: " + contentStreamURL);
            throw new RuntimeException(e);
        }
    }


    @Override
    @InterfaceAudience.Public
    public Map<String, Object> getProperties() {
        return properties;
    }

    @Override
    @InterfaceAudience.Public
    public SavedRevision getParentRevision() {
        if (parentRevID == null || parentRevID.length() == 0) {
            return null;
        }
        return document.getRevision(parentRevID);
    }

    @Override
    @InterfaceAudience.Public
    public String getParentRevisionId() {
        return parentRevID;
    }

    @Override
    @InterfaceAudience.Public
    public List<SavedRevision> getRevisionHistory() throws CouchbaseLiteException {
        // (Don't include self in the array, because this revision doesn't really exist yet)
        SavedRevision parent = getParentRevision();
        return parent != null ? parent.getRevisionHistory() : new ArrayList<SavedRevision>();
    }



}

package com.couchbase.cblite;

import com.couchbase.cblite.internal.CBLBody;

import java.util.HashMap;
import java.util.Map;

public class CBLNewRevision extends CBLRevisionBase {

    // private CBLBody body;
    private String parentRevID;
    private Map<String, Object> properties;

    protected CBLNewRevision(CBLDocument document, CBLRevision parentRevision) {

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

    public void setProperties(Map<String,Object> properties) {
        this.properties = properties;
    }

    public void setDeleted(boolean deleted) {
        if (deleted == true) {
            properties.put("_deleted", true);
        }
        else {
            properties.remove("_deleted");
        }
    }

    public CBLRevision getParentRevision() {
        if (parentRevID == null || parentRevID.length() == 0) {
            return null;
        }
        return document.getRevision(parentRevID);
    }

    public String getParentRevisionId() {
        return parentRevID;
    }

    public CBLRevision save() throws CBLiteException {
        return document.putProperties(properties, parentRevID);
    }

    public void addAttachment(CBLAttachment attachment, String name) {
        Map<String, Object> attachments =  (Map<String, Object>) properties.get("_attachments");
        attachments.put(name, attachment);
        properties.put("_attachments", attachments);
        attachment.setName(name);
        attachment.setRevision(this);
    }

    public void removeAttachmentNamed(String name) {
        addAttachment(null, name);
    }


}

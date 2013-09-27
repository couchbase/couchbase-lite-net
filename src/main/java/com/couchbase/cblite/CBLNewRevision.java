package com.couchbase.cblite;

import java.util.HashMap;
import java.util.Map;

public class CBLNewRevision extends CBLRevisionBase {

    private CBLBody body;
    private String parentRevID;

    protected CBLNewRevision(CBLDocument document, CBLRevision parent) {

        super(document);

        parentRevID = parent.getRevId();

        // note: in the iOS version, this was being converted from an immutable -> mutable map.
        // but since the original map is already mutable, not doing anything special here.
        body = parent.getBody();

        if (body == null) {
            Map properties = new HashMap<String, Object>();
            properties.put("_id", document.getDocumentID());
            properties.put("_rev", parentRevID);
            body = new CBLBody(properties);
        }

    }

    public void setProperties(Map<String,Object> properties) {
        this.body = new CBLBody(properties);
    }

    public void setDeleted(boolean deleted) {
        this.deleted = deleted;
    }

    public CBLRevision parentRevision() {
        if (parentRevID == null || parentRevID.length() == 0) {
            return null;
        }
        return document.revisionWithID(parentRevID);
    }

    public CBLRevision save() throws CBLiteException {
        return document.putProperties(body.getProperties(), parentRevID);
    }

    public void addAttachment(CBLAttachment attachment) {
        // TODO: implement
        throw new RuntimeException("Not implemented");
    }

    public void removeAttachmentNamed(String attachmentName) {
        // TODO: implement
        throw new RuntimeException("Not implemented");
    }


}

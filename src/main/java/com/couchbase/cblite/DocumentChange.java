package com.couchbase.cblite;

import java.net.URL;

public class DocumentChange {

    public DocumentChange(String documentId, String revisionId, boolean isCurrentRevision, boolean isConflict, URL sourceUrl) {
        this.documentId = documentId;
        this.revisionId = revisionId;
        this.isCurrentRevision = isCurrentRevision;
        this.isConflict = isConflict;
        this.sourceUrl = sourceUrl;
    }

    private String documentId;
    private String revisionId;
    private boolean isCurrentRevision;
    private boolean isConflict;
    private URL sourceUrl;

    public String getDocumentId() {
        return documentId;
    }

    public String getRevisionId() {
        return revisionId;
    }

    public boolean isCurrentRevision() {
        return isCurrentRevision;
    }

    public boolean isConflict() {
        return isConflict;
    }

    public URL getSourceUrl() {
        return sourceUrl;
    }
}

package com.couchbase.cblite;

import com.couchbase.cblite.internal.CBLRevisionInternal;

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

    public static DocumentChange tempFactory(CBLRevisionInternal revisionInternal, URL sourceUrl) {

        boolean isCurrentRevFixMe = false; // TODO: fix this to have a real value
        boolean isConflictRevFixMe = false; // TODO: fix this to have a real value

        DocumentChange change = new DocumentChange(
                revisionInternal.getDocId(),
                revisionInternal.getRevId(),
                isCurrentRevFixMe,
                isConflictRevFixMe,
                sourceUrl);

        return change;
    }

}

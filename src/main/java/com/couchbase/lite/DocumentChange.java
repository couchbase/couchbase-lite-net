package com.couchbase.lite;

import com.couchbase.lite.internal.RevisionInternal;
import com.couchbase.lite.internal.InterfaceAudience;

import java.net.URL;

public class DocumentChange {

    DocumentChange(RevisionInternal revisionInternal, boolean isCurrentRevision, boolean isConflict, URL sourceUrl) {
        this.revisionInternal = revisionInternal;
        this.isCurrentRevision = isCurrentRevision;
        this.isConflict = isConflict;
        this.sourceUrl = sourceUrl;
    }

    private RevisionInternal revisionInternal;
    private boolean isCurrentRevision;
    private boolean isConflict;
    private URL sourceUrl;

    public String getDocumentId() {
        return revisionInternal.getDocId();
    }

    public String getRevisionId() {
        return revisionInternal.getRevId();
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

    @InterfaceAudience.Private
    public RevisionInternal getRevisionInternal() {
        return revisionInternal;
    }

    public static DocumentChange tempFactory(RevisionInternal revisionInternal, URL sourceUrl) {

        boolean isCurrentRevFixMe = false; // TODO: fix this to have a real value
        boolean isConflictRevFixMe = false; // TODO: fix this to have a real value

        DocumentChange change = new DocumentChange(
                revisionInternal,
                isCurrentRevFixMe,
                isConflictRevFixMe,
                sourceUrl);

        return change;
    }

}

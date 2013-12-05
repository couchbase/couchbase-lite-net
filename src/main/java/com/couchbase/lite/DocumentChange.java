package com.couchbase.lite;

import com.couchbase.lite.internal.CBLRevisionInternal;
import com.couchbase.lite.internal.InterfaceAudience;

import java.net.URL;

public class DocumentChange {

    DocumentChange(CBLRevisionInternal revisionInternal, boolean isCurrentRevision, boolean isConflict, URL sourceUrl) {
        this.revisionInternal = revisionInternal;
        this.isCurrentRevision = isCurrentRevision;
        this.isConflict = isConflict;
        this.sourceUrl = sourceUrl;
    }

    private CBLRevisionInternal revisionInternal;
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
    public CBLRevisionInternal getRevisionInternal() {
        return revisionInternal;
    }

    public static DocumentChange tempFactory(CBLRevisionInternal revisionInternal, URL sourceUrl) {

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

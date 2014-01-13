package com.couchbase.lite;

import com.couchbase.lite.internal.RevisionInternal;
import com.couchbase.lite.internal.InterfaceAudience;

import java.net.URL;

public class DocumentChange {

    @InterfaceAudience.Private
    DocumentChange(RevisionInternal addedRevision, RevisionInternal winningRevision, boolean isConflict, URL sourceUrl) {
        this.addedRevision = addedRevision;
        this.winningRevision = winningRevision;
        this.isConflict = isConflict;
        this.sourceUrl = sourceUrl;
    }

    private RevisionInternal addedRevision;
    private RevisionInternal winningRevision;
    private boolean isConflict;
    private URL sourceUrl;

    public String getDocumentId() {
        return addedRevision.getDocId();
    }

    public String getRevisionId() {
        return addedRevision.getRevId();
    }

    public boolean isCurrentRevision() {
        return winningRevision != null && addedRevision.getRevId().equals(winningRevision.getRevId());
    }

    public boolean isConflict() {
        return isConflict;
    }

    public URL getSourceUrl() {
        return sourceUrl;
    }

    @InterfaceAudience.Private
    public RevisionInternal getAddedRevision() {
        return addedRevision;
    }

    @InterfaceAudience.Private
    RevisionInternal getWinningRevision() {
        return winningRevision;
    }

    public static DocumentChange tempFactory(RevisionInternal revisionInternal, URL sourceUrl, boolean inConflict) {

        DocumentChange change = new DocumentChange(
                revisionInternal,
                null,  // TODO: fix winning revision here
                inConflict,
                sourceUrl);

        return change;
    }

}

package com.couchbase.lite;

import com.couchbase.lite.internal.RevisionInternal;
import com.couchbase.lite.internal.InterfaceAudience;
import com.couchbase.lite.util.Log;

import java.net.URL;

/**
 * Provides details about a Document change.
 */
public class DocumentChange {

    /**
     * @exclude
     */
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

    @InterfaceAudience.Public
    public String getDocumentId() {
        return addedRevision.getDocId();
    }

    @InterfaceAudience.Public
    public String getRevisionId() {
        return addedRevision.getRevId();
    }

    @InterfaceAudience.Public
    public boolean isCurrentRevision() {
        return winningRevision != null && addedRevision.getRevId().equals(winningRevision.getRevId());
    }

    @InterfaceAudience.Public
    public boolean isConflict() {
        return isConflict;
    }

    @InterfaceAudience.Public
    public URL getSourceUrl() {
        return sourceUrl;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public RevisionInternal getAddedRevision() {
        return addedRevision;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    RevisionInternal getWinningRevision() {
        return winningRevision;
    }

    @InterfaceAudience.Public
    public String toString() {
        try {
            return String.format(
                    "docId: %s rev: %s isConflict: %s sourceUrl: %s",
                    getDocumentId(),
                    getRevisionId(),
                    isConflict(),
                    getSourceUrl()
            );
        } catch (Exception e) {
            Log.e(Database.TAG, "Error in DocumentChange.toString()", e);
            return super.toString();
        }
    }

}

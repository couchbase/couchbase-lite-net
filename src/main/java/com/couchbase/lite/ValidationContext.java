package com.couchbase.lite;

import com.couchbase.lite.internal.CBLRevisionInternal;

/**
 * Context passed into a ValidationBlock.
 */
public interface ValidationContext {

    /**
     * The contents of the current revision of the document, or nil if this is a new document.
     */
    CBLRevisionInternal getCurrentRevision() throws CouchbaseLiteException;

    /**
     * The type of HTTP status to report, if the validate block returns NO.
     * The default value is 403 ("Forbidden").
     */
    Status getErrorType();
    void setErrorType(Status status);

    /**
     * The error message to return in the HTTP response, if the validate block returns NO.
     * The default value is "invalid document".
     */
    String getErrorMessage();
    void setErrorMessage(String message);

}

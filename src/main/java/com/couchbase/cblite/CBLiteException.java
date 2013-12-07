package com.couchbase.cblite;

public class CBLiteException extends Exception {

    private CBLStatus status;

    public CBLiteException(CBLStatus status) {
        this.status = status;
    }

    public CBLiteException(String detailMessage, CBLStatus status) {
        super(detailMessage);
        this.status = status;
    }

    public CBLiteException(String detailMessage, Throwable throwable, CBLStatus status) {
        super(detailMessage, throwable);
        this.status = status;
    }

    public CBLiteException(Throwable throwable, CBLStatus status) {
        super(throwable);
        this.status = status;
    }

    public CBLStatus getCBLStatus() {
        return status;
    }

}

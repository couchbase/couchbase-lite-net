package com.couchbase.lite;

public class CBLiteException extends Exception {

    private CBLStatus status;

    public CBLiteException(int statusCode) {
        this.status = new CBLStatus(statusCode);
    }

    public CBLiteException(CBLStatus status) {
        this.status = status;
    }

    public CBLiteException(String detailMessage, CBLStatus status) {
        super(detailMessage);
        this.status = status;
    }

    public CBLiteException(String detailMessage, int statusCode) {
        this(detailMessage, new CBLStatus(statusCode));
    }

    public CBLiteException(String detailMessage, Throwable throwable, CBLStatus status) {
        super(detailMessage, throwable);
        this.status = status;
    }

    public CBLiteException(Throwable throwable, CBLStatus status) {
        super(throwable);
        this.status = status;
    }

    public CBLiteException(Throwable throwable, int statusCode) {
        super(throwable);
        this.status = new CBLStatus(statusCode);
    }

    public CBLStatus getCBLStatus() {
        return status;
    }

}

package com.couchbase.lite;

public class CBLiteException extends Exception {

    private Status status;

    public CBLiteException(int statusCode) {
        this.status = new Status(statusCode);
    }

    public CBLiteException(Status status) {
        this.status = status;
    }

    public CBLiteException(String detailMessage, Status status) {
        super(detailMessage);
        this.status = status;
    }

    public CBLiteException(String detailMessage, int statusCode) {
        this(detailMessage, new Status(statusCode));
    }

    public CBLiteException(String detailMessage, Throwable throwable, Status status) {
        super(detailMessage, throwable);
        this.status = status;
    }

    public CBLiteException(Throwable throwable, Status status) {
        super(throwable);
        this.status = status;
    }

    public CBLiteException(Throwable throwable, int statusCode) {
        super(throwable);
        this.status = new Status(statusCode);
    }

    public Status getCBLStatus() {
        return status;
    }

}

package com.couchbase.lite;

public class CouchbaseLiteException extends Exception {

    private Status status;

    public CouchbaseLiteException(int statusCode) {
        this.status = new Status(statusCode);
    }

    public CouchbaseLiteException(Status status) {
        this.status = status;
    }

    public CouchbaseLiteException(String detailMessage, Status status) {
        super(detailMessage);
        this.status = status;
    }

    public CouchbaseLiteException(String detailMessage, int statusCode) {
        this(detailMessage, new Status(statusCode));
    }

    public CouchbaseLiteException(String detailMessage, Throwable throwable, Status status) {
        super(detailMessage, throwable);
        this.status = status;
    }

    public CouchbaseLiteException(Throwable throwable, Status status) {
        super(throwable);
        this.status = status;
    }

    public CouchbaseLiteException(Throwable throwable, int statusCode) {
        super(throwable);
        this.status = new Status(statusCode);
    }

    public Status getCBLStatus() {
        return status;
    }

    // TODO: override toString to print out status code

}

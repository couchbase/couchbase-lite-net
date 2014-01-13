package com.couchbase.lite;

public class ManagerOptions {

    /**
     *  No modifications to databases are allowed.
     */
    private boolean readOnly;

    public ManagerOptions() {
    }

    public boolean isReadOnly() {
        return readOnly;
    }

    public void setReadOnly(boolean readOnly) {
        this.readOnly = readOnly;
    }

}

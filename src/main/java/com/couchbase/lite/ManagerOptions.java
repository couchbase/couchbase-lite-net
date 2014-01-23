package com.couchbase.lite;

/**
 * Option flags for Manager initialization.
 */
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

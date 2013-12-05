package com.couchbase.lite;

public class ManagerOptions {

    /**
     *  No modifications to databases are allowed.
     */
    private boolean readOnly;

    /**
     * Persistent replications will not run (until/unless startPersistentReplications is called.)
     */
    private boolean noReplicator;

    public ManagerOptions(boolean readOnly, boolean noReplicator) {
        this.readOnly = readOnly;
        this.noReplicator = noReplicator;
    }

    public boolean isReadOnly() {
        return readOnly;
    }

    public void setReadOnly(boolean readOnly) {
        this.readOnly = readOnly;
    }

    public boolean isNoReplicator() {
        return noReplicator;
    }

    public void setNoReplicator(boolean noReplicator) {
        this.noReplicator = noReplicator;
    }

}

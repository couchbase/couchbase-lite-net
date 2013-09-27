package com.couchbase.cblite;

public class CBLManagerOptions {

    private boolean readOnly;
    private boolean noReplicator;

    public CBLManagerOptions(boolean readOnly, boolean noReplicator) {
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

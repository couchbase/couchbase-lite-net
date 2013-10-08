package com.couchbase.cblite;

public class CBLLiveQuery extends CBLQuery {

    // just added this to remove compiler error. TODO: fix this
    CBLLiveQuery(CBLDatabase database, CBLView view) {
        super(database, view);
    }

    public void start() {
        // TODO
        throw new RuntimeException("Not implemented");
    }

    public void stop() {
        // TODO
        throw new RuntimeException("Not implemented");
    }

    public boolean waitForRows() {
        // TODO
        throw new RuntimeException("Not implemented");
    }

    public void addChangeListener(CBLLiveQueryChangedFunction liveQueryChangedFunction) {
        // TODO
        throw new RuntimeException("Not implemented");
    }


}

package com.couchbase.cblite;

public class CBLLiveQuery extends CBLQuery {

    CBLLiveQuery(CBLQuery query) {
        super(query.getDatabase(), query.getView());
        setLimit(query.getLimit());
        setSkip(query.getSkip());
        setStartKey(query.getStartKey());
        setEndKey(query.getEndKey());
        setDescending(query.isDescending());
        setPrefetch(query.isPrefetch());
        setKeys(query.getKeys());
        setGroupLevel(query.getGroupLevel());
        setMapOnly(query.isMapOnly());
        setStartKeyDocId(query.getStartKeyDocId());
        setEndKeyDocId(query.getEndKeyDocId());
        setStale(query.getStale());
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
